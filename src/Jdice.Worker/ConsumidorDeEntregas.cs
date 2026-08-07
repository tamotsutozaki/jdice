using System.Text.Json;
using Jdice.Application.Abstractions;
using Jdice.Application.Campaigns;
using Jdice.Infrastructure.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Jdice.Worker;

/// <summary>
/// Consome as entregas repartidas na fila. É o que permite escalar o envio:
/// vários workers dividem o mesmo lote.
/// <para>
/// Confirma a mensagem só depois de processar. Se o worker morrer no meio, o
/// broker devolve a mensagem para outro — e a proteção contra enviar duas
/// vezes é a mesma de sempre: a entrega só é processada se ninguém a tiver
/// assumido antes.
/// </para>
/// </summary>
public sealed class ConsumidorDeEntregas(
    RabbitMqConnection conexao,
    IOptions<RabbitMqOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<ConsumidorDeEntregas> logger) : BackgroundService
{
    private readonly RabbitMqOptions _options = options.Value;

    private IChannel? _canal;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation(
                "Fila desligada; os disparos serão processados em série pelo próprio job.");

            return;
        }

        await EsperarBrokerAsync(stoppingToken);

        _canal = await conexao.CreateChannelAsync(stoppingToken);
        await conexao.DeclareTopologyAsync(_canal, stoppingToken);

        // Sem limite de prefetch, um worker puxaria a fila inteira e os outros
        // ficariam ociosos — o oposto do que a fila existe para resolver.
        await _canal.BasicQosAsync(0, _options.Prefetch, global: false, stoppingToken);

        var consumidor = new AsyncEventingBasicConsumer(_canal);
        consumidor.ReceivedAsync += ProcessarMensagemAsync;

        await _canal.BasicConsumeAsync(
            _options.Queue,
            // Confirmação manual: só depois de processar.
            autoAck: false,
            consumidor,
            stoppingToken);

        logger.LogInformation("Consumindo entregas da fila {Fila}.", _options.Queue);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessarMensagemAsync(object sender, BasicDeliverEventArgs evento)
    {
        var trabalho = JsonSerializer.Deserialize<DeliveryWork>(evento.Body.Span);

        if (trabalho is null)
        {
            // Mensagem ilegível não melhora com nova tentativa: vai direto
            // para a fila morta em vez de circular para sempre.
            await _canal!.BasicRejectAsync(evento.DeliveryTag, requeue: false);
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<CampaignProcessor>();

            await processor.ProcessDeliveryAsync(trabalho.CampaignId, trabalho.DeliveryId);

            await _canal!.BasicAckAsync(evento.DeliveryTag, multiple: false);
        }
        catch (Exception excecao)
        {
            logger.LogError(
                excecao,
                "Falha ao processar a entrega {Entrega} do disparo {Disparo}.",
                trabalho.DeliveryId,
                trabalho.CampaignId);

            // Devolve para a fila uma vez. Se falhar de novo, o próprio
            // controle de tentativas da entrega encerra o assunto.
            await _canal!.BasicNackAsync(evento.DeliveryTag, multiple: false, requeue: !evento.Redelivered);
        }
    }

    /// <summary>O broker pode demorar mais que o worker para ficar de pé.</summary>
    private async Task EsperarBrokerAsync(CancellationToken stoppingToken)
    {
        var limite = DateTimeOffset.UtcNow.AddMinutes(2);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var canal = await conexao.CreateChannelAsync(stoppingToken);
                return;
            }
            catch (Exception excecao) when (excecao is not OperationCanceledException)
            {
                if (DateTimeOffset.UtcNow > limite)
                {
                    throw;
                }

                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_canal is not null)
        {
            await _canal.CloseAsync(cancellationToken);
            await _canal.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}

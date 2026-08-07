using System.Text.Json;
using Jdice.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Jdice.Infrastructure.Messaging;

public sealed class RabbitMqDeliveryQueue(
    RabbitMqConnection conexao,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqDeliveryQueue> logger) : IDeliveryQueue
{
    private readonly RabbitMqOptions _options = options.Value;

    public bool IsEnabled => _options.Enabled;

    public async Task PublishAsync(
        IReadOnlyCollection<DeliveryWork> trabalhos,
        CancellationToken cancellationToken = default)
    {
        if (trabalhos.Count == 0)
        {
            return;
        }

        await using var canal = await conexao.CreateChannelAsync(cancellationToken);
        await conexao.DeclareTopologyAsync(canal, cancellationToken);

        var propriedades = new BasicProperties
        {
            // Mensagem persistente: um reinício do broker não pode fazer
            // sumir entregas que já foram aceitas.
            Persistent = true,
            ContentType = "application/json"
        };

        foreach (var trabalho in trabalhos)
        {
            await canal.BasicPublishAsync(
                _options.Exchange,
                routingKey: _options.Queue,
                mandatory: false,
                basicProperties: propriedades,
                body: JsonSerializer.SerializeToUtf8Bytes(trabalho),
                cancellationToken: cancellationToken);
        }

        logger.LogInformation("{Total} entregas publicadas na fila.", trabalhos.Count);
    }
}

/// <summary>Usada quando o RabbitMQ está desligado: o disparo roda em série.</summary>
public sealed class NoDeliveryQueue : IDeliveryQueue
{
    public bool IsEnabled => false;

    public Task PublishAsync(
        IReadOnlyCollection<DeliveryWork> trabalhos,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}

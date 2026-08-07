using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Jdice.Infrastructure.Messaging;

/// <summary>
/// Conexão compartilhada com o RabbitMQ, criada na primeira necessidade.
/// <para>
/// Abrir uma conexão por mensagem seria caro — o protocolo faz negociação e
/// autenticação a cada vez. A conexão é uma; os canais, vários.
/// </para>
/// </summary>
public sealed class RabbitMqConnection(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqConnection> logger) : IAsyncDisposable
{
    private readonly RabbitMqOptions _options = options.Value;
    private readonly SemaphoreSlim _trava = new(1, 1);

    private IConnection? _conexao;

    public RabbitMqOptions Options => _options;

    public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default)
    {
        var conexao = await ObterConexaoAsync(cancellationToken);

        return await conexao.CreateChannelAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Declara a topologia. Chamado por quem publica e por quem consome, porque
    /// qualquer um dos dois pode subir primeiro — e um consumidor esperando
    /// numa fila que não existe ficaria parado em silêncio.
    /// </summary>
    public async Task DeclareTopologyAsync(IChannel canal, CancellationToken cancellationToken = default)
    {
        await canal.ExchangeDeclareAsync(
            _options.Exchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        // Mensagem recusada em definitivo vai para a fila morta em vez de
        // sumir: sem isso, uma entrega problemática desapareceria sem deixar
        // rastro de que existiu.
        await canal.QueueDeclareAsync(
            _options.DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await canal.QueueDeclareAsync(
            _options.Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = _options.DeadLetterQueue
            },
            cancellationToken: cancellationToken);

        await canal.QueueBindAsync(
            _options.Queue,
            _options.Exchange,
            routingKey: _options.Queue,
            cancellationToken: cancellationToken);
    }

    private async Task<IConnection> ObterConexaoAsync(CancellationToken cancellationToken)
    {
        if (_conexao is { IsOpen: true })
        {
            return _conexao;
        }

        await _trava.WaitAsync(cancellationToken);

        try
        {
            if (_conexao is { IsOpen: true })
            {
                return _conexao;
            }

            var fabrica = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.Username,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
            };

            _conexao = await fabrica.CreateConnectionAsync("jdice", cancellationToken);

            logger.LogInformation("Conectado ao RabbitMQ em {Host}:{Porta}", _options.Host, _options.Port);

            return _conexao;
        }
        finally
        {
            _trava.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_conexao is not null)
        {
            await _conexao.CloseAsync();
            await _conexao.DisposeAsync();
        }

        _trava.Dispose();
    }
}

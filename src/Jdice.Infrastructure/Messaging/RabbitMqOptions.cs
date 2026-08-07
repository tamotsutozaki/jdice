namespace Jdice.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    /// <summary>
    /// Desligado, o disparo é processado em série dentro do próprio job. Para
    /// volumes pequenos isso basta, e é o que mantém o sistema funcionando sem
    /// depender de mais uma peça de infraestrutura.
    /// </summary>
    public bool Enabled { get; set; }

    public string Host { get; set; } = "rabbitmq";

    public int Port { get; set; } = 5672;

    public string Username { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string VirtualHost { get; set; } = "/";

    public string Exchange { get; set; } = "jdice.deliveries";

    public string Queue { get; set; } = "jdice.deliveries";

    /// <summary>
    /// Quantas mensagens um consumidor recebe antes de confirmar. Baixo de
    /// propósito: cada mensagem é um e-mail, e acumular dezenas em memória só
    /// aumentaria o estrago se o processo morresse.
    /// </summary>
    public ushort Prefetch { get; set; } = 10;

    /// <summary>Fila para onde vão as mensagens que falharam de vez.</summary>
    public string DeadLetterQueue { get; set; } = "jdice.deliveries.dead";
}

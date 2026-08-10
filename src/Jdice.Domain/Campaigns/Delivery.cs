namespace Jdice.Domain.Campaigns;

/// <summary>
/// Uma tentativa de envio que falhou. Guardar cada uma responde "por que
/// falhou?" com mais que o último erro: mostra que a 1ª foi timeout e a 3ª uma
/// recusa, por exemplo.
/// </summary>
public sealed record DeliveryAttempt(int Numero, DateTimeOffset Quando, string Erro);

public enum DeliveryStatus
{
    /// <summary>Aguardando o worker pegar.</summary>
    Pending = 0,

    /// <summary>Um worker assumiu esta entrega e está processando.</summary>
    Sending = 1,

    Sent = 2,

    /// <summary>Falhou e não será mais tentada.</summary>
    Failed = 3,

    /// <summary>Não foi enviada de propósito — destinatário descadastrado, por exemplo.</summary>
    Skipped = 4
}

/// <summary>
/// O envio para uma pessoa específica. Existe uma por destinatário para que a
/// pergunta "quem recebeu e quem não recebeu?" tenha resposta, e para que uma
/// falha isolada não derrube o disparo inteiro.
/// <para>
/// As transições de estado são explícitas e verificam a situação anterior. Com
/// vários workers em paralelo, redelivery de fila e retry automático, a mesma
/// entrega pode ser processada duas vezes — e a segunda vez seria um e-mail
/// duplicado para uma pessoa real, que não tem desfazer.
/// </para>
/// </summary>
public sealed class Delivery
{
    public const int MaximumAttempts = 3;

    // Exigido pelo EF Core para materializar a entidade.
    private Delivery()
    {
        Email = null!;
        Error = null!;
    }

    private Delivery(
        Guid id,
        Guid campaignId,
        Guid recipientId,
        string email,
        DateTimeOffset createdAt)
    {
        Id = id;
        CampaignId = campaignId;
        RecipientId = recipientId;
        Email = email;
        Status = DeliveryStatus.Pending;
        Error = string.Empty;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid CampaignId { get; private set; }

    public Guid RecipientId { get; private set; }

    /// <summary>
    /// Copiado do destinatário no momento do disparo. Se ele mudar de e-mail
    /// depois, o histórico continua mostrando para onde a mensagem foi.
    /// </summary>
    public string Email { get; private set; }

    public DeliveryStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public string Error { get; private set; }

    /// <summary>Uma linha por tentativa que falhou, na ordem em que aconteceram.</summary>
    public IReadOnlyList<DeliveryAttempt> AttemptLog { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public static Delivery Create(
        Guid campaignId,
        Guid recipientId,
        string email,
        DateTimeOffset createdAt) =>
        new(Guid.CreateVersion7(createdAt), campaignId, recipientId, email, createdAt);

    /// <summary>
    /// Assume a entrega para processamento.
    /// </summary>
    /// <returns>
    /// <c>false</c> quando outro worker já assumiu ou já concluiu — nesse caso
    /// quem chamou deve simplesmente desistir, sem tratar como erro.
    /// </returns>
    public bool TryStart()
    {
        if (Status is not DeliveryStatus.Pending)
        {
            return false;
        }

        Status = DeliveryStatus.Sending;
        Attempts++;

        return true;
    }

    public void MarkSent(DateTimeOffset sentAt)
    {
        Status = DeliveryStatus.Sent;
        SentAt = sentAt;
        Error = string.Empty;
    }

    /// <summary>
    /// Registra a falha. Volta para pendente enquanto houver tentativa
    /// sobrando; passando do limite, desiste de vez para não insistir
    /// eternamente num endereço que não existe.
    /// </summary>
    public void MarkFailed(string motivo, DateTimeOffset quando)
    {
        Error = Encurtar(motivo);

        // Lista reconstruída em vez de mutada: o EF trata a coluna jsonb como um
        // valor só, e trocar a referência é o que ele detecta como mudança.
        AttemptLog = [.. AttemptLog, new DeliveryAttempt(Attempts, quando, Error)];

        Status = Attempts >= MaximumAttempts ? DeliveryStatus.Failed : DeliveryStatus.Pending;
    }

    public void Skip(string motivo)
    {
        Status = DeliveryStatus.Skipped;
        Error = Encurtar(motivo);
    }

    /// <summary>Erro de SMTP pode vir com um relatório inteiro; a coluna guarda o essencial.</summary>
    private static string Encurtar(string motivo)
    {
        var limpo = motivo?.Trim() ?? string.Empty;

        return limpo.Length > 500 ? limpo[..500] : limpo;
    }
}

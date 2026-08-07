namespace Jdice.Domain.Campaigns;

public enum CampaignStatus
{
    /// <summary>Criada e aguardando a hora — imediata ou agendada.</summary>
    Scheduled = 0,

    /// <summary>O worker começou a processar as entregas.</summary>
    Running = 1,

    /// <summary>Todas as entregas chegaram a um desfecho.</summary>
    Completed = 2,

    /// <summary>Cancelada antes de começar.</summary>
    Cancelled = 3
}

/// <summary>
/// Um disparo: um modelo, uma versão dele e um conjunto de destinatários.
/// <para>
/// Aponta para uma <c>TemplateVersion</c> específica, não para o modelo. É o
/// que permite responder, meses depois, exatamente qual conteúdo foi enviado —
/// mesmo que o modelo tenha ganhado versões novas desde então.
/// </para>
/// </summary>
public sealed class Campaign
{
    public const int MaximumSubjectLength = 300;

    private readonly List<Delivery> _deliveries = [];

    // Exigido pelo EF Core para materializar a entidade.
    private Campaign()
    {
        Name = null!;
        Subject = null!;
        FromName = null!;
        TimeZone = null!;
        SharedValues = null!;
    }

    private Campaign(
        Guid id,
        string name,
        Guid templateId,
        Guid templateVersionId,
        int templateVersionNumber,
        string subject,
        string fromName,
        IReadOnlyDictionary<string, string> sharedValues,
        DateTimeOffset scheduledFor,
        string timeZone,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        TemplateId = templateId;
        TemplateVersionId = templateVersionId;
        TemplateVersionNumber = templateVersionNumber;
        Subject = subject;
        FromName = fromName;
        SharedValues = sharedValues;
        ScheduledFor = scheduledFor;
        TimeZone = timeZone;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        Status = CampaignStatus.Scheduled;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public Guid TemplateId { get; private set; }

    public Guid TemplateVersionId { get; private set; }

    /// <summary>Guardado junto para exibir "v3" sem precisar carregar a versão.</summary>
    public int TemplateVersionNumber { get; private set; }

    public string Subject { get; private set; }

    public string FromName { get; private set; }

    /// <summary>
    /// Valores informados no disparo, iguais para todo mundo. As variáveis que
    /// mudam por pessoa vêm dos campos do destinatário e têm precedência sobre
    /// estes.
    /// </summary>
    public IReadOnlyDictionary<string, string> SharedValues { get; private set; }

    /// <summary>Quando deve sair. Igual a <see cref="CreatedAt"/> num disparo imediato.</summary>
    public DateTimeOffset ScheduledFor { get; private set; }

    /// <summary>
    /// Fuso escolhido por quem agendou, guardado por extenso. Sem ele,
    /// "amanhã às 9h" perde o sentido quando alguém abre a tela em outro fuso.
    /// </summary>
    public string TimeZone { get; private set; }

    public CampaignStatus Status { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Identificador do job no agendador, para conseguir cancelar depois.</summary>
    public string? JobId { get; private set; }

    public IReadOnlyList<Delivery> Deliveries => _deliveries;

    public bool IsScheduledForFuture => Status is CampaignStatus.Scheduled;

    public static Campaign Create(
        string name,
        Guid templateId,
        Guid templateVersionId,
        int templateVersionNumber,
        string subject,
        string? fromName,
        IReadOnlyDictionary<string, string>? sharedValues,
        DateTimeOffset scheduledFor,
        string? timeZone,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        var assunto = subject?.Trim() ?? string.Empty;

        if (assunto.Length == 0)
        {
            throw new ArgumentException("O assunto do e-mail é obrigatório.", nameof(subject));
        }

        if (assunto.Length > MaximumSubjectLength)
        {
            throw new ArgumentException(
                $"O assunto deve ter no máximo {MaximumSubjectLength} caracteres.",
                nameof(subject));
        }

        var titulo = name?.Trim() ?? string.Empty;

        return new Campaign(
            Guid.CreateVersion7(createdAt),
            titulo.Length > 0 ? titulo : assunto,
            templateId,
            templateVersionId,
            templateVersionNumber,
            assunto,
            fromName?.Trim() ?? string.Empty,
            sharedValues ?? new Dictionary<string, string>(),
            scheduledFor,
            timeZone?.Trim() ?? "UTC",
            createdBy,
            createdAt);
    }

    public Delivery AddDelivery(Guid recipientId, string email, DateTimeOffset createdAt)
    {
        var delivery = Delivery.Create(Id, recipientId, email, createdAt);

        _deliveries.Add(delivery);

        return delivery;
    }

    public void AssignJob(string jobId) => JobId = jobId;

    /// <returns><c>false</c> se outro worker já começou — evita reiniciar o disparo.</returns>
    public bool TryStart(DateTimeOffset startedAt)
    {
        if (Status is not CampaignStatus.Scheduled)
        {
            return false;
        }

        Status = CampaignStatus.Running;
        StartedAt = startedAt;

        return true;
    }

    public void Complete(DateTimeOffset completedAt)
    {
        Status = CampaignStatus.Completed;
        CompletedAt = completedAt;
    }

    /// <summary>
    /// Cancela o disparo. Só vale antes de começar: depois que a primeira
    /// mensagem saiu, não há como trazê-la de volta.
    /// </summary>
    public void Cancel()
    {
        if (Status is not CampaignStatus.Scheduled)
        {
            throw new InvalidOperationException(
                "Só é possível cancelar um disparo que ainda não começou.");
        }

        Status = CampaignStatus.Cancelled;
    }

    /// <summary>Muda a hora de saída. Mesma regra do cancelamento.</summary>
    public void Reschedule(DateTimeOffset scheduledFor, string? timeZone)
    {
        if (Status is not CampaignStatus.Scheduled)
        {
            throw new InvalidOperationException(
                "Só é possível reagendar um disparo que ainda não começou.");
        }

        ScheduledFor = scheduledFor;

        if (!string.IsNullOrWhiteSpace(timeZone))
        {
            TimeZone = timeZone.Trim();
        }
    }
}

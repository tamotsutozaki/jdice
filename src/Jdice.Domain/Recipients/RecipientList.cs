namespace Jdice.Domain.Recipients;

/// <summary>
/// Agrupamento de destinatários — "Clientes ativos", "Newsletter". A lista
/// referencia quem já existe; ela não é dona do destinatário.
/// </summary>
public sealed class RecipientList
{
    public const int MaximumNameLength = 120;
    public const int MaximumDescriptionLength = 300;

    private readonly List<RecipientListMember> _members = [];

    // Exigido pelo EF Core para materializar a entidade.
    private RecipientList()
    {
        Name = null!;
        Description = null!;
    }

    private RecipientList(
        Guid id,
        string name,
        string description,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Description = description;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string Description { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<RecipientListMember> Members => _members;

    public static RecipientList Create(
        string name,
        string? description,
        Guid createdBy,
        DateTimeOffset createdAt) =>
        new(
            Guid.CreateVersion7(createdAt),
            NormalizeName(name),
            NormalizeDescription(description),
            createdBy,
            createdAt);

    public void UpdateDetails(string name, string? description, DateTimeOffset updatedAt)
    {
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        UpdatedAt = updatedAt;
    }

    /// <summary>Idempotente: incluir de novo quem já está não duplica nem falha.</summary>
    public void Add(Guid recipientId, DateTimeOffset addedAt)
    {
        if (_members.Any(membro => membro.RecipientId == recipientId))
        {
            return;
        }

        _members.Add(new RecipientListMember(Id, recipientId, addedAt));
        UpdatedAt = addedAt;
    }

    public void Remove(Guid recipientId, DateTimeOffset removedAt)
    {
        var membro = _members.FirstOrDefault(item => item.RecipientId == recipientId);

        if (membro is not null)
        {
            _members.Remove(membro);
            UpdatedAt = removedAt;
        }
    }

    private static string NormalizeName(string? name)
    {
        var normalizado = name?.Trim() ?? string.Empty;

        if (normalizado.Length == 0)
        {
            throw new ArgumentException("O nome da lista é obrigatório.", nameof(name));
        }

        if (normalizado.Length > MaximumNameLength)
        {
            throw new ArgumentException(
                $"O nome da lista deve ter no máximo {MaximumNameLength} caracteres.",
                nameof(name));
        }

        return normalizado;
    }

    private static string NormalizeDescription(string? description)
    {
        var normalizado = description?.Trim() ?? string.Empty;

        return normalizado.Length > MaximumDescriptionLength
            ? normalizado[..MaximumDescriptionLength]
            : normalizado;
    }
}

/// <summary>Participação de um destinatário numa lista.</summary>
public sealed class RecipientListMember
{
    // Exigido pelo EF Core.
    private RecipientListMember()
    {
    }

    internal RecipientListMember(Guid listId, Guid recipientId, DateTimeOffset addedAt)
    {
        RecipientListId = listId;
        RecipientId = recipientId;
        AddedAt = addedAt;
    }

    public Guid RecipientListId { get; private set; }

    public Guid RecipientId { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }
}

namespace Jdice.Domain.Templates;

/// <summary>
/// Modelo de e-mail. Guarda o que serve para localizá-lo — nome, categoria,
/// tags — e o histórico de versões do conteúdo.
/// <para>
/// Estes metadados são editáveis de propósito, ao contrário do conteúdo: eles
/// não vão dentro do e-mail enviado, e congelá-los faria corrigir um erro de
/// digitação numa tag gerar uma versão de HTML idêntica à anterior, poluindo o
/// histórico que a imutabilidade existe para proteger.
/// </para>
/// </summary>
public sealed class Template
{
    public const int MaximumNameLength = 120;
    public const int MaximumCategoryLength = 60;
    public const int MaximumTagLength = 40;
    public const int MaximumTagCount = 10;

    private readonly List<TemplateVersion> _versions = [];

    // Exigido pelo EF Core para materializar a entidade.
    private Template()
    {
        Name = null!;
        Category = null!;
        Tags = null!;
    }

    private Template(
        Guid id,
        string name,
        string category,
        IReadOnlyList<string> tags,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Category = category;
        Tags = tags;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string Category { get; private set; }

    public IReadOnlyList<string> Tags { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Quando foi tirado de circulação, ou <c>null</c> se ativo.</summary>
    public DateTimeOffset? ArchivedAt { get; private set; }

    public bool IsArchived => ArchivedAt is not null;

    public IReadOnlyList<TemplateVersion> Versions => _versions;

    /// <summary>Versão mais recente — a que será usada num disparo novo.</summary>
    public TemplateVersion? CurrentVersion =>
        _versions.Count == 0 ? null : _versions.MaxBy(version => version.Number);

    public static Template Create(
        string name,
        string category,
        IEnumerable<string>? tags,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        var template = new Template(
            Guid.CreateVersion7(createdAt),
            NormalizeName(name),
            NormalizeCategory(category),
            NormalizeTags(tags),
            createdBy,
            createdAt);

        return template;
    }

    /// <summary>
    /// Congela uma nova versão do conteúdo. É o único caminho para mudar o que
    /// o modelo envia — não existe edição de versão já criada.
    /// </summary>
    public TemplateVersion AddVersion(
        string html,
        IReadOnlyList<string> variables,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        if (IsArchived)
        {
            throw new InvalidOperationException(
                "Não é possível adicionar versão a um modelo arquivado.");
        }

        var version = TemplateVersion.Create(
            Id,
            (CurrentVersion?.Number ?? 0) + 1,
            html,
            variables,
            createdBy,
            createdAt);

        _versions.Add(version);
        UpdatedAt = createdAt;

        return version;
    }

    /// <summary>Altera apenas os dados de catalogação. O conteúdo é intocado.</summary>
    public void UpdateDetails(
        string name,
        string category,
        IEnumerable<string>? tags,
        DateTimeOffset updatedAt)
    {
        Name = NormalizeName(name);
        Category = NormalizeCategory(category);
        Tags = NormalizeTags(tags);
        UpdatedAt = updatedAt;
    }

    /// <summary>Idempotente: arquivar de novo não altera a data original.</summary>
    public void Archive(DateTimeOffset archivedAt)
    {
        if (ArchivedAt is null)
        {
            ArchivedAt = archivedAt;
            UpdatedAt = archivedAt;
        }
    }

    public void Unarchive(DateTimeOffset updatedAt)
    {
        if (ArchivedAt is not null)
        {
            ArchivedAt = null;
            UpdatedAt = updatedAt;
        }
    }

    private static string NormalizeName(string? name)
    {
        var normalized = name?.Trim() ?? string.Empty;

        if (normalized.Length == 0)
        {
            throw new ArgumentException("O nome do modelo é obrigatório.", nameof(name));
        }

        if (normalized.Length > MaximumNameLength)
        {
            throw new ArgumentException(
                $"O nome do modelo deve ter no máximo {MaximumNameLength} caracteres.",
                nameof(name));
        }

        return normalized;
    }

    private static string NormalizeCategory(string? category)
    {
        var normalized = category?.Trim() ?? string.Empty;

        if (normalized.Length > MaximumCategoryLength)
        {
            throw new ArgumentException(
                $"A categoria deve ter no máximo {MaximumCategoryLength} caracteres.",
                nameof(category));
        }

        // Categoria em branco é aceitável e vira "Sem categoria" na exibição;
        // exigir classificação na criação só faria as pessoas digitarem lixo.
        return normalized;
    }

    private static IReadOnlyList<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return [];
        }

        var normalized = tags
            .Select(tag => tag?.Trim() ?? string.Empty)
            .Where(tag => tag.Length > 0)
            // Sem distinção de caixa: "Marketing" e "marketing" seriam a mesma
            // gaveta para quem procura, e duas para quem filtra.
            .DistinctBy(tag => tag.ToLowerInvariant())
            .ToList();

        if (normalized.Any(tag => tag.Length > MaximumTagLength))
        {
            throw new ArgumentException(
                $"Cada tag deve ter no máximo {MaximumTagLength} caracteres.",
                nameof(tags));
        }

        if (normalized.Count > MaximumTagCount)
        {
            throw new ArgumentException(
                $"São permitidas no máximo {MaximumTagCount} tags.",
                nameof(tags));
        }

        return normalized;
    }
}

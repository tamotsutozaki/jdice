namespace Jdice.Domain.Recipients;

/// <summary>
/// Quem recebe os e-mails. Existe uma vez só no sistema, identificado pelo
/// e-mail, e participa de quantas listas for preciso.
/// <para>
/// O descadastro é do destinatário, não da lista: quem pede para não receber
/// mais espera parar de receber — e não parar só na lista de onde veio a
/// mensagem que o incomodou.
/// </para>
/// </summary>
public sealed class Recipient
{
    public const int MaximumNameLength = 200;
    public const int MaximumFieldCount = 30;
    public const int MaximumFieldNameLength = 60;
    public const int MaximumFieldValueLength = 500;

    // Exigido pelo EF Core para materializar a entidade.
    private Recipient()
    {
        Email = null!;
        Name = null!;
        Fields = null!;
    }

    private Recipient(
        Guid id,
        string email,
        string name,
        IReadOnlyDictionary<string, string> fields,
        DateTimeOffset createdAt)
    {
        Id = id;
        Email = email;
        Name = name;
        Fields = fields;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; }

    public string Name { get; private set; }

    /// <summary>
    /// Colunas livres trazidas na importação — empresa, plano, cidade. São
    /// elas que permitem personalizar o e-mail além do nome.
    /// </summary>
    public IReadOnlyDictionary<string, string> Fields { get; private set; }

    /// <summary>Quando pediu para não receber mais, ou <c>null</c> se aceita receber.</summary>
    public DateTimeOffset? UnsubscribedAt { get; private set; }

    public bool IsSubscribed => UnsubscribedAt is null;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Recipient Create(
        string email,
        string? name,
        IReadOnlyDictionary<string, string>? fields,
        DateTimeOffset createdAt)
    {
        var normalizedEmail = NormalizeEmail(email);

        if (!IsValidEmail(normalizedEmail))
        {
            throw new ArgumentException($"E-mail inválido: '{email}'.", nameof(email));
        }

        return new Recipient(
            Guid.CreateVersion7(createdAt),
            normalizedEmail,
            NormalizeName(name),
            NormalizeFields(fields),
            createdAt);
    }

    /// <summary>
    /// Atualiza nome e campos. Usado quando a importação reencontra alguém que
    /// já estava cadastrado.
    /// </summary>
    public void Update(
        string? name,
        IReadOnlyDictionary<string, string>? fields,
        DateTimeOffset updatedAt)
    {
        Name = NormalizeName(name);
        Fields = NormalizeFields(fields);
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Mescla campos novos sobre os existentes, preservando os que o arquivo
    /// não trouxe — uma planilha só com "email;plano" não deve apagar a
    /// empresa cadastrada antes.
    /// </summary>
    public void MergeFields(IReadOnlyDictionary<string, string>? fields, DateTimeOffset updatedAt)
    {
        if (fields is null || fields.Count == 0)
        {
            return;
        }

        var mesclado = new Dictionary<string, string>(
            Fields.ToDictionary(par => par.Key, par => par.Value),
            StringComparer.OrdinalIgnoreCase);

        foreach (var (nome, valor) in fields)
        {
            mesclado[nome] = valor;
        }

        Fields = NormalizeFields(mesclado);
        UpdatedAt = updatedAt;
    }

    /// <summary>Idempotente: pedir de novo não altera a data original.</summary>
    public void Unsubscribe(DateTimeOffset unsubscribedAt)
    {
        if (UnsubscribedAt is null)
        {
            UnsubscribedAt = unsubscribedAt;
            UpdatedAt = unsubscribedAt;
        }
    }

    public void Resubscribe(DateTimeOffset updatedAt)
    {
        if (UnsubscribedAt is not null)
        {
            UnsubscribedAt = null;
            UpdatedAt = updatedAt;
        }
    }

    public static string NormalizeEmail(string? email) =>
        email?.Trim().ToLowerInvariant() ?? string.Empty;

    /// <summary>
    /// Validação deliberadamente simples: exige uma arroba com texto dos dois
    /// lados e um ponto no domínio. Regras mais elaboradas rejeitam endereços
    /// legítimos, e o teste definitivo de um e-mail é a entrega.
    /// </summary>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 320)
        {
            return false;
        }

        var partes = email.Split('@');

        if (partes.Length != 2)
        {
            return false;
        }

        var (local, dominio) = (partes[0], partes[1]);

        return local.Length > 0
            && dominio.Length > 2
            && dominio.Contains('.')
            && !dominio.StartsWith('.')
            && !dominio.EndsWith('.')
            && !email.Contains(' ');
    }

    private static string NormalizeName(string? name)
    {
        var normalizado = name?.Trim() ?? string.Empty;

        return normalizado.Length > MaximumNameLength
            ? normalizado[..MaximumNameLength]
            : normalizado;
    }

    private static IReadOnlyDictionary<string, string> NormalizeFields(
        IReadOnlyDictionary<string, string>? fields)
    {
        if (fields is null || fields.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var normalizado = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (nome, valor) in fields)
        {
            var chave = nome?.Trim() ?? string.Empty;

            if (chave.Length == 0 || normalizado.Count >= MaximumFieldCount)
            {
                continue;
            }

            if (chave.Length > MaximumFieldNameLength)
            {
                chave = chave[..MaximumFieldNameLength];
            }

            var conteudo = valor?.Trim() ?? string.Empty;

            normalizado[chave] = conteudo.Length > MaximumFieldValueLength
                ? conteudo[..MaximumFieldValueLength]
                : conteudo;
        }

        return normalizado;
    }
}

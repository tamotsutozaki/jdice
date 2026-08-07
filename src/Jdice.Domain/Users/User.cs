namespace Jdice.Domain.Users;

/// <summary>
/// Conta de acesso ao sistema. O e-mail é o identificador de login e é
/// guardado sempre normalizado, para que "Pedro@X.com" e "pedro@x.com " não
/// virem duas contas.
/// </summary>
public sealed class User
{
    // Exigido pelo EF Core para materializar a entidade.
    private User()
    {
        Email = null!;
        PasswordHash = null!;
    }

    private User(Guid id, string email, string passwordHash, UserRole role, DateTimeOffset createdAt)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; }

    public string PasswordHash { get; private set; }

    public UserRole Role { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Quando a conta foi desativada, ou <c>null</c> se está ativa. A linha é
    /// preservada em vez de removida porque, a partir do momento em que
    /// existirem modelos e disparos com autor, apagar a conta transformaria
    /// "enviado por fulano" em "enviado por ninguém".
    /// </summary>
    public DateTimeOffset? DeactivatedAt { get; private set; }

    public bool IsActive => DeactivatedAt is null;

    public static User Create(string email, string passwordHash, UserRole role, DateTimeOffset createdAt)
    {
        var normalizedEmail = NormalizeEmail(email);

        if (normalizedEmail.Length == 0)
        {
            throw new ArgumentException("E-mail é obrigatório.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Hash da senha é obrigatório.", nameof(passwordHash));
        }

        // UUIDv7 derivado do próprio instante de criação: o id fica ordenável
        // por data, o que mantém o índice do Postgres sequencial em vez de
        // fragmentado como aconteceria com UUIDv4 aleatório.
        return new User(Guid.CreateVersion7(createdAt), normalizedEmail, passwordHash, role, createdAt);
    }

    public void ChangePassword(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Hash da senha é obrigatório.", nameof(passwordHash));
        }

        PasswordHash = passwordHash;
    }

    /// <summary>Idempotente: desativar de novo não altera a data original.</summary>
    public void Deactivate(DateTimeOffset deactivatedAt)
    {
        DeactivatedAt ??= deactivatedAt;
    }

    public void Reactivate()
    {
        DeactivatedAt = null;
    }

    /// <summary>
    /// Minúsculas e sem espaços nas pontas. Usada tanto ao criar quanto ao
    /// buscar, para que a comparação seja sempre entre valores no mesmo formato.
    /// </summary>
    public static string NormalizeEmail(string? email) =>
        email?.Trim().ToLowerInvariant() ?? string.Empty;
}

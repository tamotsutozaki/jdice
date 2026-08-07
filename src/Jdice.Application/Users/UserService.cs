using Jdice.Application.Abstractions;
using Jdice.Domain.Users;

namespace Jdice.Application.Users;

public sealed class UserService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    TimeProvider clock)
{
    public const int MinimumPasswordLength = 12;

    /// <exception cref="EmailAlreadyInUseException">Já existe conta com esse e-mail.</exception>
    public async Task<User> CreateAsync(
        string email,
        string password,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = User.NormalizeEmail(email);

        if (await users.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            throw new EmailAlreadyInUseException(normalizedEmail);
        }

        var user = User.Create(
            normalizedEmail,
            passwordHasher.Hash(password),
            role,
            clock.GetUtcNow());

        await users.AddAsync(user, cancellationToken);
        await users.SaveChangesAsync(cancellationToken);

        return user;
    }

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        users.FindByIdAsync(id, cancellationToken);
}

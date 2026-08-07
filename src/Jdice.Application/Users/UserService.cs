using Jdice.Application.Abstractions;
using Jdice.Domain.Users;

namespace Jdice.Application.Users;

public sealed class UserService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    TimeProvider clock)
{
    /// <exception cref="EmailAlreadyInUseException">Já existe conta com esse e-mail.</exception>
    /// <exception cref="ArgumentException">A senha não atende à política.</exception>
    public async Task<User> CreateAsync(
        string email,
        string password,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        // Validada aqui além do contrato da API: o seed também cria contas por
        // este caminho, e uma regra que só existe no DTO não vale para quem
        // chama o serviço direto.
        PasswordPolicy.EnsureValid(password, nameof(password));

        var normalizedEmail = User.NormalizeEmail(email);

        // Checagem antecipada para responder 409 sem ir ao banco à toa. Não é
        // garantia: duas requisições simultâneas passam as duas por aqui, e
        // quem de fato impede a duplicata é o índice único — cuja violação o
        // repositório traduz para a mesma exceção.
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

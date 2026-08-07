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

    public Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default) =>
        users.ListAsync(cancellationToken);

    /// <summary>
    /// Desativa a conta preservando a linha. Não remove o registro porque, a
    /// partir da Fase 4, cada conta terá modelos e disparos associados — e
    /// apagá-la transformaria "enviado por fulano" em "enviado por ninguém".
    /// </summary>
    /// <param name="requestedBy">Quem pediu a desativação, para impedir a autodesativação.</param>
    /// <exception cref="UserNotFoundException">A conta não existe.</exception>
    /// <exception cref="CannotDeactivateSelfException">Alguém tentou desativar a si mesmo.</exception>
    /// <exception cref="LastAdministratorException">Sobraria zero administrador ativo.</exception>
    public async Task DeactivateAsync(
        Guid userId,
        Guid requestedBy,
        CancellationToken cancellationToken = default)
    {
        if (userId == requestedBy)
        {
            throw new CannotDeactivateSelfException();
        }

        var user = await users.FindByIdAsync(userId, cancellationToken)
            ?? throw new UserNotFoundException(userId);

        // Já desativada: nada a fazer, e nada a reclamar. Repetir o pedido não
        // pode virar erro só porque alguém clicou duas vezes.
        if (!user.IsActive)
        {
            return;
        }

        if (user.Role == UserRole.Admin
            && await users.CountActiveAdministratorsAsync(cancellationToken) <= 1)
        {
            throw new LastAdministratorException();
        }

        user.Deactivate(clock.GetUtcNow());

        await users.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Devolve o acesso a uma conta desativada. Não tem as travas da
    /// desativação: reativar alguém nunca deixa o sistema sem administrador.
    /// </summary>
    /// <exception cref="UserNotFoundException">A conta não existe.</exception>
    public async Task ReactivateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(userId, cancellationToken)
            ?? throw new UserNotFoundException(userId);

        // Já ativa: nada a fazer, e nada a reclamar — mesma razão da
        // desativação repetida não ser erro.
        if (user.IsActive)
        {
            return;
        }

        user.Reactivate();

        await users.SaveChangesAsync(cancellationToken);
    }
}

using Jdice.Domain.Users;

namespace Jdice.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Usado pelo seed para decidir se o banco ainda está sem contas.</summary>
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    /// <summary>Todas as contas, ativas e desativadas, das mais recentes para as mais antigas.</summary>
    Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Quantos administradores ainda podem entrar. Serve para impedir que o
    /// último deles seja desativado, o que deixaria o sistema sem ninguém
    /// capaz de administrá-lo.
    /// </summary>
    Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

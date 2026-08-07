using Jdice.Application.Abstractions;
using Jdice.Application.Users;
using Jdice.Domain.Users;

namespace Jdice.UnitTests.Users;

/// <summary>
/// Fake escrito à mão em vez de mock: o comportamento que importa aqui é
/// "guarda e devolve por e-mail", e isso lê melhor como implementação do que
/// como uma pilha de setups.
/// </summary>
internal sealed class FakeUserRepository : IUserRepository
{
    private readonly List<User> _users = [];

    public int SaveChangesCount { get; private set; }

    public void Seed(params User[] users) => _users.AddRange(users);

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.SingleOrDefault(user => user.Email == email));

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.SingleOrDefault(user => user.Id == id));

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.Any(user => user.Email == email));

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.Count > 0);

    public Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<User>>(
            [.. _users.OrderByDescending(user => user.CreatedAt)]);

    public Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.Count(user => user.Role == UserRole.Admin && user.IsActive));

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        // Imita o índice único do banco: é ele, e não a checagem prévia, que
        // de fato impede duas contas com o mesmo e-mail.
        if (_users.Any(existente => existente.Email == user.Email))
        {
            throw new EmailAlreadyInUseException(user.Email);
        }

        _users.Add(user);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCount++;
        return Task.CompletedTask;
    }
}

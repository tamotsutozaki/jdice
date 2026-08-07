using Jdice.Application.Abstractions;
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

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        _users.Add(user);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCount++;
        return Task.CompletedTask;
    }
}

using Jdice.Application.Abstractions;
using Jdice.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Jdice.Infrastructure.Persistence;

public sealed class UserRepository(JdiceDbContext context) : IUserRepository
{
    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        context.Users.SingleOrDefaultAsync(user => user.Email == email, cancellationToken);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(user => user.Email == email, cancellationToken);

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await context.Users.AddAsync(user, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}

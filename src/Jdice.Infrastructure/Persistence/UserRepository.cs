using Jdice.Application.Abstractions;
using Jdice.Application.Users;
using Jdice.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Jdice.Infrastructure.Persistence;

public sealed class UserRepository(JdiceDbContext context) : IUserRepository
{
    /// <summary>Código SQLSTATE do Postgres para violação de restrição única.</summary>
    private const string UniqueViolation = "23505";

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        context.Users.SingleOrDefaultAsync(user => user.Email == email, cancellationToken);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(user => user.Email == email, cancellationToken);

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(cancellationToken);

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Add(user);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            // Só o banco impede a duplicata de verdade: entre a checagem em
            // memória e este INSERT cabe outra requisição criando o mesmo
            // e-mail. Sem traduzir aqui, essa corrida viraria 500 — culpando o
            // servidor por um conflito que tem resposta própria (409).
            context.ChangeTracker.Clear();

            throw new EmailAlreadyInUseException(EmailDaTentativa(exception));
        }
    }

    private static string EmailDaTentativa(DbUpdateException exception) =>
        exception.Entries
            .Select(entry => entry.Entity)
            .OfType<User>()
            .Select(user => user.Email)
            .FirstOrDefault()
        ?? string.Empty;
}

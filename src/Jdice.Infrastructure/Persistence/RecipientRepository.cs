using Jdice.Application.Abstractions;
using Jdice.Application.Recipients;
using Jdice.Domain.Recipients;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Jdice.Infrastructure.Persistence;

public sealed class RecipientRepository(JdiceDbContext context) : IRecipientRepository
{
    private const string UniqueViolation = "23505";

    /// <summary>Teto para não deixar uma página gigante derrubar a memória.</summary>
    private const int TamanhoMaximoDaPagina = 200;

    public async Task<PagedResult<Recipient>> ListAsync(
        RecipientFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = context.Recipients.AsQueryable();

        if (filter.ListaId is { } listaId)
        {
            var idsDaLista = context
                .Set<RecipientListMember>()
                .Where(membro => membro.RecipientListId == listaId)
                .Select(membro => membro.RecipientId);

            query = query.Where(recipient => idsDaLista.Contains(recipient.Id));
        }

        if (!filter.IncluirDescadastrados)
        {
            query = query.Where(recipient => recipient.UnsubscribedAt == null);
        }

        if (!string.IsNullOrWhiteSpace(filter.Busca))
        {
            var busca = filter.Busca.Trim();

            query = query.Where(recipient =>
                EF.Functions.ILike(recipient.Email, $"%{busca}%")
                || EF.Functions.ILike(recipient.Name, $"%{busca}%"));
        }

        var total = await query.CountAsync(cancellationToken);

        var pagina = Math.Max(1, filter.Pagina);
        var tamanho = Math.Clamp(filter.TamanhoDaPagina, 1, TamanhoMaximoDaPagina);

        var itens = await query
            .OrderBy(recipient => recipient.Email)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .ToListAsync(cancellationToken);

        return new PagedResult<Recipient>(itens, total, pagina, tamanho);
    }

    public Task<Recipient?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Recipients.SingleOrDefaultAsync(recipient => recipient.Id == id, cancellationToken);

    public Task<Recipient?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken = default) =>
        context.Recipients.SingleOrDefaultAsync(
            recipient => recipient.Email == email,
            cancellationToken);

    public async Task<IReadOnlyDictionary<string, Recipient>> FindByEmailsAsync(
        IReadOnlyCollection<string> emails,
        CancellationToken cancellationToken = default)
    {
        if (emails.Count == 0)
        {
            return new Dictionary<string, Recipient>();
        }

        var encontrados = await context.Recipients
            .Where(recipient => emails.Contains(recipient.Email))
            .ToListAsync(cancellationToken);

        return encontrados.ToDictionary(
            recipient => recipient.Email,
            recipient => recipient,
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<RecipientList>> ListsOfAsync(
        Guid recipientId,
        CancellationToken cancellationToken = default)
    {
        var idsDasListas = context
            .Set<RecipientListMember>()
            .Where(membro => membro.RecipientId == recipientId)
            .Select(membro => membro.RecipientListId);

        return await context.RecipientLists
            .Where(lista => idsDasListas.Contains(lista.Id))
            .OrderBy(lista => lista.Name)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(Recipient recipient, CancellationToken cancellationToken = default)
    {
        context.Recipients.Add(recipient);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(
        IReadOnlyCollection<Recipient> recipients,
        CancellationToken cancellationToken = default)
    {
        context.Recipients.AddRange(recipients);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Recipient recipient, CancellationToken cancellationToken = default)
    {
        context.Recipients.Remove(recipient);
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
            context.ChangeTracker.Clear();

            // Duas importações do mesmo e-mail ao mesmo tempo passam pela
            // verificação prévia e colidem no índice único. Conflito tem
            // resposta própria; não é falha do servidor.
            throw new RecipientEmailAlreadyInUseException(EmailDaTentativa(exception));
        }
    }

    private static string EmailDaTentativa(DbUpdateException exception) =>
        exception.Entries
            .Select(entry => entry.Entity)
            .OfType<Recipient>()
            .Select(recipient => recipient.Email)
            .FirstOrDefault()
        ?? string.Empty;
}

public sealed class RecipientListRepository(JdiceDbContext context) : IRecipientListRepository
{
    public async Task<IReadOnlyList<(RecipientList Lista, int TotalDeMembros, int TotalAtivos)>>
        ListWithCountsAsync(CancellationToken cancellationToken = default)
    {
        // As contagens saem numa consulta só: buscar as listas e depois contar
        // os membros de cada uma seria uma ida ao banco por lista.
        var dados = await context.RecipientLists
            .OrderBy(lista => lista.Name)
            .Select(lista => new
            {
                Lista = lista,
                TotalDeMembros = context.Set<RecipientListMember>()
                    .Count(membro => membro.RecipientListId == lista.Id),
                TotalAtivos = context.Set<RecipientListMember>()
                    .Count(membro =>
                        membro.RecipientListId == lista.Id
                        && context.Recipients.Any(recipient =>
                            recipient.Id == membro.RecipientId
                            && recipient.UnsubscribedAt == null))
            })
            .ToListAsync(cancellationToken);

        return [.. dados.Select(item => (item.Lista, item.TotalDeMembros, item.TotalAtivos))];
    }

    public Task<RecipientList?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.RecipientLists
            .Include(lista => lista.Members)
            .SingleOrDefaultAsync(lista => lista.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(
        string name,
        Guid? exceptId = null,
        CancellationToken cancellationToken = default) =>
        context.RecipientLists.AnyAsync(
            lista => lista.Name.ToLower() == name.ToLower()
                && (exceptId == null || lista.Id != exceptId),
            cancellationToken);

    public Task AddAsync(RecipientList list, CancellationToken cancellationToken = default)
    {
        context.RecipientLists.Add(list);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(RecipientList list, CancellationToken cancellationToken = default)
    {
        context.RecipientLists.Remove(list);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}

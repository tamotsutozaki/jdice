using Jdice.Domain.Recipients;

namespace Jdice.Application.Abstractions;

public sealed record PagedResult<T>(IReadOnlyList<T> Itens, int Total, int Pagina, int TamanhoDaPagina)
{
    public int TotalDePaginas =>
        TamanhoDaPagina <= 0 ? 0 : (int)Math.Ceiling(Total / (double)TamanhoDaPagina);
}

/// <param name="ListaId">Restringe a uma lista. Nulo traz todos.</param>
/// <param name="Busca">Filtra por e-mail ou nome, sem distinção de caixa.</param>
/// <param name="IncluirDescadastrados">Descadastrados ficam fora por padrão.</param>
public sealed record RecipientFilter(
    Guid? ListaId = null,
    string? Busca = null,
    bool IncluirDescadastrados = false,
    int Pagina = 1,
    int TamanhoDaPagina = 50);

public interface IRecipientRepository
{
    Task<PagedResult<Recipient>> ListAsync(
        RecipientFilter filter,
        CancellationToken cancellationToken = default);

    Task<Recipient?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Recipient?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Traz de uma vez os destinatários dos e-mails informados. A importação
    /// processa milhares de linhas; consultar um por um seria uma ida ao banco
    /// por linha.
    /// </summary>
    Task<IReadOnlyDictionary<string, Recipient>> FindByEmailsAsync(
        IReadOnlyCollection<string> emails,
        CancellationToken cancellationToken = default);

    /// <summary>Em quais listas o destinatário está.</summary>
    Task<IReadOnlyList<RecipientList>> ListsOfAsync(
        Guid recipientId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Recipient recipient, CancellationToken cancellationToken = default);

    Task AddRangeAsync(
        IReadOnlyCollection<Recipient> recipients,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(Recipient recipient, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IRecipientListRepository
{
    Task<IReadOnlyList<(RecipientList Lista, int TotalDeMembros, int TotalAtivos)>> ListWithCountsAsync(
        CancellationToken cancellationToken = default);

    Task<RecipientList?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(
        string name,
        Guid? exceptId = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(RecipientList list, CancellationToken cancellationToken = default);

    Task RemoveAsync(RecipientList list, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

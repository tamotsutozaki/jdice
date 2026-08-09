using Jdice.Application.Abstractions;
using Jdice.Domain.Recipients;

namespace Jdice.Application.Recipients;

public sealed class RecipientListService(
    IRecipientListRepository lists,
    IRecipientRepository recipients,
    TimeProvider clock)
{
    public Task<IReadOnlyList<(RecipientList Lista, int TotalDeMembros, int TotalAtivos)>> ListAsync(
        CancellationToken cancellationToken = default) =>
        lists.ListWithCountsAsync(cancellationToken);

    public async Task<RecipientList> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await lists.FindByIdAsync(id, cancellationToken)
        ?? throw new RecipientListNotFoundException(id);

    /// <summary>Total de membros e quantos ainda recebem, para uma lista só.</summary>
    public Task<(int TotalDeMembros, int TotalAtivos)> CountMembersAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        lists.CountMembersAsync(id, cancellationToken);

    public async Task<RecipientList> CreateAsync(
        string nome,
        string? descricao,
        Guid createdBy,
        CancellationToken cancellationToken = default)
    {
        if (await lists.NameExistsAsync(nome.Trim(), cancellationToken: cancellationToken))
        {
            throw new RecipientListNameAlreadyInUseException(nome.Trim());
        }

        var lista = RecipientList.Create(nome, descricao, createdBy, clock.GetUtcNow());

        await lists.AddAsync(lista, cancellationToken);
        await lists.SaveChangesAsync(cancellationToken);

        return lista;
    }

    public async Task<RecipientList> UpdateAsync(
        Guid id,
        string nome,
        string? descricao,
        CancellationToken cancellationToken = default)
    {
        var lista = await GetAsync(id, cancellationToken);

        if (await lists.NameExistsAsync(nome.Trim(), id, cancellationToken))
        {
            throw new RecipientListNameAlreadyInUseException(nome.Trim());
        }

        lista.UpdateDetails(nome, descricao, clock.GetUtcNow());

        await lists.SaveChangesAsync(cancellationToken);

        return lista;
    }

    /// <summary>
    /// Apaga a lista. Os destinatários continuam no sistema: a lista é um
    /// agrupamento, não a dona das pessoas.
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var lista = await GetAsync(id, cancellationToken);

        await lists.RemoveAsync(lista, cancellationToken);
        await lists.SaveChangesAsync(cancellationToken);
    }

    public async Task AddMemberAsync(
        Guid listaId,
        Guid recipientId,
        CancellationToken cancellationToken = default)
    {
        var lista = await GetAsync(listaId, cancellationToken);

        _ = await recipients.FindByIdAsync(recipientId, cancellationToken)
            ?? throw new RecipientNotFoundException(recipientId);

        lista.Add(recipientId, clock.GetUtcNow());

        await lists.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Tira da lista sem apagar o destinatário — sair de um agrupamento é
    /// diferente de pedir para não receber mais nada.
    /// </summary>
    public async Task RemoveMemberAsync(
        Guid listaId,
        Guid recipientId,
        CancellationToken cancellationToken = default)
    {
        var lista = await GetAsync(listaId, cancellationToken);

        lista.Remove(recipientId, clock.GetUtcNow());

        await lists.SaveChangesAsync(cancellationToken);
    }
}

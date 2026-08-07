using Jdice.Application.Abstractions;
using Jdice.Domain.Recipients;

namespace Jdice.Application.Recipients;

public sealed class RecipientService(
    IRecipientRepository recipients,
    IRecipientListRepository lists,
    ICsvRecipientReader csv,
    TimeProvider clock)
{
    public Task<PagedResult<Recipient>> ListAsync(
        RecipientFilter filter,
        CancellationToken cancellationToken = default) =>
        recipients.ListAsync(filter, cancellationToken);

    public async Task<Recipient> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await recipients.FindByIdAsync(id, cancellationToken)
        ?? throw new RecipientNotFoundException(id);

    public Task<IReadOnlyList<RecipientList>> ListsOfAsync(
        Guid recipientId,
        CancellationToken cancellationToken = default) =>
        recipients.ListsOfAsync(recipientId, cancellationToken);

    public async Task<Recipient> CreateAsync(
        string email,
        string? nome,
        IReadOnlyDictionary<string, string>? campos,
        Guid? adicionarNaLista = null,
        CancellationToken cancellationToken = default)
    {
        var normalizado = Recipient.NormalizeEmail(email);

        if (await recipients.FindByEmailAsync(normalizado, cancellationToken) is not null)
        {
            throw new RecipientEmailAlreadyInUseException(normalizado);
        }

        var agora = clock.GetUtcNow();
        var recipient = Recipient.Create(email, nome, campos, agora);

        await recipients.AddAsync(recipient, cancellationToken);

        if (adicionarNaLista is { } listaId)
        {
            var lista = await lists.FindByIdAsync(listaId, cancellationToken)
                ?? throw new RecipientListNotFoundException(listaId);

            lista.Add(recipient.Id, agora);
        }

        await recipients.SaveChangesAsync(cancellationToken);

        return recipient;
    }

    public async Task<Recipient> UpdateAsync(
        Guid id,
        string? nome,
        IReadOnlyDictionary<string, string>? campos,
        CancellationToken cancellationToken = default)
    {
        var recipient = await GetAsync(id, cancellationToken);

        recipient.Update(nome, campos, clock.GetUtcNow());

        await recipients.SaveChangesAsync(cancellationToken);

        return recipient;
    }

    /// <summary>
    /// Marca que a pessoa não quer mais receber. Vale para todas as listas —
    /// quem pede para sair espera parar de receber, não parar só na lista de
    /// onde veio a mensagem que a incomodou.
    /// </summary>
    public async Task UnsubscribeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var recipient = await GetAsync(id, cancellationToken);

        recipient.Unsubscribe(clock.GetUtcNow());

        await recipients.SaveChangesAsync(cancellationToken);
    }

    public async Task ResubscribeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var recipient = await GetAsync(id, cancellationToken);

        recipient.Resubscribe(clock.GetUtcNow());

        await recipients.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var recipient = await GetAsync(id, cancellationToken);

        await recipients.RemoveAsync(recipient, cancellationToken);
        await recipients.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Importa uma planilha, opcionalmente incluindo todo mundo numa lista.
    /// Quem já existe é atualizado em vez de duplicado, e os campos novos são
    /// mesclados sobre os antigos.
    /// </summary>
    public async Task<ImportResult> ImportAsync(
        Stream conteudo,
        Guid? listaId,
        CancellationToken cancellationToken = default)
    {
        RecipientList? lista = null;

        if (listaId is { } id)
        {
            lista = await lists.FindByIdAsync(id, cancellationToken)
                ?? throw new RecipientListNotFoundException(id);
        }

        var leitura = csv.Read(conteudo);

        if (leitura.Linhas.Count == 0)
        {
            return new ImportResult(
                0,
                0,
                0,
                0,
                [.. leitura.Erros.Select(erro => new ImportIssue(erro.Linha, erro.Motivo))],
                leitura.ColunasLivres);
        }

        var agora = clock.GetUtcNow();

        // Uma consulta só para todos os e-mails do arquivo: com milhares de
        // linhas, verificar um a um seria uma ida ao banco por linha.
        var existentes = await recipients.FindByEmailsAsync(
            [.. leitura.Linhas.Select(linha => linha.Email)],
            cancellationToken);

        var novos = new List<Recipient>();
        var recusados = leitura.Erros
            .Select(erro => new ImportIssue(erro.Linha, erro.Motivo))
            .ToList();

        var criados = 0;
        var atualizados = 0;
        var jaNaLista = 0;

        foreach (var linha in leitura.Linhas)
        {
            if (existentes.TryGetValue(linha.Email, out var existente))
            {
                if (linha.Nome.Length > 0 || linha.Campos.Count > 0)
                {
                    // Nome só é sobrescrito quando o arquivo traz um; e os
                    // campos são mesclados para não apagar o que a planilha
                    // anterior tinha trazido.
                    existente.Update(
                        linha.Nome.Length > 0 ? linha.Nome : existente.Name,
                        existente.Fields,
                        agora);

                    existente.MergeFields(linha.Campos, agora);
                }

                atualizados++;

                if (lista is not null)
                {
                    var estava = lista.Members.Any(membro => membro.RecipientId == existente.Id);
                    lista.Add(existente.Id, agora);

                    if (estava)
                    {
                        jaNaLista++;
                    }
                }

                continue;
            }

            try
            {
                var recipient = Recipient.Create(linha.Email, linha.Nome, linha.Campos, agora);

                novos.Add(recipient);
                criados++;

                lista?.Add(recipient.Id, agora);
            }
            catch (ArgumentException excecao)
            {
                // O leitor já filtra e-mail inválido; isto cobre o que só o
                // domínio sabe recusar, sem derrubar a importação inteira.
                recusados.Add(new ImportIssue(linha.Numero, excecao.Message));
            }
        }

        if (novos.Count > 0)
        {
            await recipients.AddRangeAsync(novos, cancellationToken);
        }

        await recipients.SaveChangesAsync(cancellationToken);

        return new ImportResult(
            leitura.Linhas.Count + leitura.Erros.Count(erro => erro.Linha > 0),
            criados,
            atualizados,
            jaNaLista,
            recusados,
            leitura.ColunasLivres);
    }
}

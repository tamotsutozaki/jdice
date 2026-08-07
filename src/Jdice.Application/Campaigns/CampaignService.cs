using Jdice.Application.Abstractions;
using Jdice.Application.Recipients;
using Jdice.Application.Templates;
using Jdice.Domain.Campaigns;
using Jdice.Domain.Recipients;

namespace Jdice.Application.Campaigns;

/// <param name="ListaIds">Listas cujos destinatários ativos entram no disparo.</param>
/// <param name="DestinatarioIds">Pessoas específicas, somadas às listas.</param>
public sealed record CampaignRequest(
    string Nome,
    Guid TemplateId,
    Guid? TemplateVersionId,
    string Assunto,
    string? Remetente,
    IReadOnlyDictionary<string, string>? ValoresComuns,
    IReadOnlyList<Guid> ListaIds,
    IReadOnlyList<Guid> DestinatarioIds,
    DateTimeOffset? Agendar,
    string? FusoHorario);

public sealed class CampaignService(
    ICampaignRepository campaigns,
    ITemplateRepository templates,
    IRecipientRepository recipients,
    ICampaignScheduler scheduler,
    TimeProvider clock)
{
    public Task<PagedResult<Campaign>> ListAsync(
        CampaignFilter filter,
        CancellationToken cancellationToken = default) =>
        campaigns.ListAsync(filter, cancellationToken);

    public async Task<Campaign> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await campaigns.FindHeaderAsync(id, cancellationToken)
        ?? throw new CampaignNotFoundException(id);

    public Task<DeliverySummary> SummaryAsync(Guid id, CancellationToken cancellationToken = default) =>
        campaigns.SummaryAsync(id, cancellationToken);

    public Task<IReadOnlyList<Delivery>> DeliveriesAsync(
        Guid id,
        int limite = 200,
        CancellationToken cancellationToken = default) =>
        campaigns.ListDeliveriesAsync(id, limite, cancellationToken);

    /// <summary>
    /// Monta o disparo e agenda o processamento. Não envia nada: quem envia é
    /// o worker, e a requisição volta assim que o trabalho está registrado.
    /// </summary>
    public async Task<Campaign> CreateAsync(
        CampaignRequest request,
        Guid createdBy,
        CancellationToken cancellationToken = default)
    {
        var template = await templates.FindByIdAsync(request.TemplateId, cancellationToken)
            ?? throw new TemplateNotFoundException(request.TemplateId);

        var versao = request.TemplateVersionId is { } versaoId
            ? template.Versions.FirstOrDefault(v => v.Id == versaoId)
            : template.CurrentVersion;

        if (versao is null)
        {
            throw new CampaignStateException("O modelo escolhido não tem nenhuma versão.");
        }

        if (template.IsArchived)
        {
            throw new CampaignStateException("Não é possível disparar um modelo arquivado.");
        }

        var destinatarios = await ReunirDestinatariosAsync(request, cancellationToken);

        if (destinatarios.Count == 0)
        {
            throw new NoRecipientsException();
        }

        var agora = clock.GetUtcNow();
        var quando = request.Agendar ?? agora;

        var campaign = Campaign.Create(
            request.Nome,
            template.Id,
            versao.Id,
            versao.Number,
            request.Assunto,
            request.Remetente,
            request.ValoresComuns,
            quando,
            request.FusoHorario,
            createdBy,
            agora);

        foreach (var destinatario in destinatarios)
        {
            campaign.AddDelivery(destinatario.Id, destinatario.Email, agora);
        }

        await campaigns.AddAsync(campaign, cancellationToken);
        await campaigns.SaveChangesAsync(cancellationToken);

        // Agendado depois de gravar: um job que dispare antes de a campanha
        // existir no banco não encontraria nada para processar.
        campaign.AssignJob(scheduler.Schedule(campaign.Id, quando));

        await campaigns.SaveChangesAsync(cancellationToken);

        return campaign;
    }

    public async Task CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var campaign = await campaigns.FindHeaderAsync(id, cancellationToken)
            ?? throw new CampaignNotFoundException(id);

        try
        {
            campaign.Cancel();
        }
        catch (InvalidOperationException excecao)
        {
            throw new CampaignStateException(excecao.Message);
        }

        if (campaign.JobId is { } jobId)
        {
            scheduler.Cancel(jobId);
        }

        await campaigns.SaveChangesAsync(cancellationToken);
    }

    public async Task RescheduleAsync(
        Guid id,
        DateTimeOffset quando,
        string? fusoHorario,
        CancellationToken cancellationToken = default)
    {
        var campaign = await campaigns.FindHeaderAsync(id, cancellationToken)
            ?? throw new CampaignNotFoundException(id);

        try
        {
            campaign.Reschedule(quando, fusoHorario);
        }
        catch (InvalidOperationException excecao)
        {
            throw new CampaignStateException(excecao.Message);
        }

        // Cancela o job antigo antes de criar o novo: deixar os dois valendo
        // faria a campanha ser processada duas vezes.
        if (campaign.JobId is { } jobId)
        {
            scheduler.Cancel(jobId);
        }

        campaign.AssignJob(scheduler.Schedule(campaign.Id, quando));

        await campaigns.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Junta as pessoas das listas com as escolhidas uma a uma, remove
    /// repetidas e descarta quem pediu para não receber mais.
    /// </summary>
    private async Task<IReadOnlyList<Recipient>> ReunirDestinatariosAsync(
        CampaignRequest request,
        CancellationToken cancellationToken)
    {
        var reunidos = new Dictionary<Guid, Recipient>();

        foreach (var listaId in request.ListaIds)
        {
            var pagina = 1;

            while (true)
            {
                var lote = await recipients.ListAsync(
                    new RecipientFilter(listaId, null, IncluirDescadastrados: false, pagina, 200),
                    cancellationToken);

                foreach (var pessoa in lote.Itens)
                {
                    reunidos[pessoa.Id] = pessoa;
                }

                if (pagina >= lote.TotalDePaginas)
                {
                    break;
                }

                pagina++;
            }
        }

        foreach (var destinatarioId in request.DestinatarioIds)
        {
            var pessoa = await recipients.FindByIdAsync(destinatarioId, cancellationToken);

            // Quem se descadastrou não entra nem sendo escolhido diretamente:
            // a decisão é da pessoa, não de quem monta o disparo.
            if (pessoa is not null && pessoa.IsSubscribed)
            {
                reunidos[pessoa.Id] = pessoa;
            }
        }

        return [.. reunidos.Values];
    }
}

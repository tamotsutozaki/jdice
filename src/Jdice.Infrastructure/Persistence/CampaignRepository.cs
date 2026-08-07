using Jdice.Application.Abstractions;
using Jdice.Domain.Campaigns;
using Microsoft.EntityFrameworkCore;

namespace Jdice.Infrastructure.Persistence;

public sealed class CampaignRepository(JdiceDbContext context) : ICampaignRepository
{
    private const int TamanhoMaximoDaPagina = 100;

    public async Task<PagedResult<Campaign>> ListAsync(
        CampaignFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = context.Campaigns.AsQueryable();

        if (filter.Situacao is { } situacao)
        {
            query = query.Where(campaign => campaign.Status == situacao);
        }

        var total = await query.CountAsync(cancellationToken);

        var pagina = Math.Max(1, filter.Pagina);
        var tamanho = Math.Clamp(filter.TamanhoDaPagina, 1, TamanhoMaximoDaPagina);

        var itens = await query
            .OrderByDescending(campaign => campaign.ScheduledFor)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .ToListAsync(cancellationToken);

        return new PagedResult<Campaign>(itens, total, pagina, tamanho);
    }

    public Task<Campaign?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Campaigns
            .Include(campaign => campaign.Deliveries)
            .SingleOrDefaultAsync(campaign => campaign.Id == id, cancellationToken);

    public Task<Campaign?> FindHeaderAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Campaigns.SingleOrDefaultAsync(campaign => campaign.Id == id, cancellationToken);

    public async Task<DeliverySummary> SummaryAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        // Contagem no banco, agrupada: carregar as entregas só para contá-las
        // seria trazer milhares de linhas para a memória à toa.
        var porSituacao = await context.Deliveries
            .Where(delivery => delivery.CampaignId == campaignId)
            .GroupBy(delivery => delivery.Status)
            .Select(grupo => new { Situacao = grupo.Key, Quantidade = grupo.Count() })
            .ToListAsync(cancellationToken);

        int Contar(DeliveryStatus situacao) =>
            porSituacao.FirstOrDefault(item => item.Situacao == situacao)?.Quantidade ?? 0;

        return new DeliverySummary(
            porSituacao.Sum(item => item.Quantidade),
            Contar(DeliveryStatus.Pending),
            Contar(DeliveryStatus.Sending),
            Contar(DeliveryStatus.Sent),
            Contar(DeliveryStatus.Failed),
            Contar(DeliveryStatus.Skipped));
    }

    public async Task<IReadOnlyList<Delivery>> NextPendingAsync(
        Guid campaignId,
        int quantidade,
        Guid? depoisDe = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.Deliveries
            .Where(delivery =>
                delivery.CampaignId == campaignId && delivery.Status == DeliveryStatus.Pending);

        if (depoisDe is { } cursor)
        {
            query = query.Where(delivery => delivery.Id > cursor);
        }

        return await query
            .OrderBy(delivery => delivery.Id)
            .Take(quantidade)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Delivery>> ListDeliveriesAsync(
        Guid campaignId,
        int limite,
        CancellationToken cancellationToken = default) =>
        await context.Deliveries
            .Where(delivery => delivery.CampaignId == campaignId)
            .OrderBy(delivery => delivery.Email)
            .Take(limite)
            .ToListAsync(cancellationToken);

    public Task<Delivery?> FindDeliveryAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default) =>
        context.Deliveries.SingleOrDefaultAsync(
            delivery => delivery.Id == deliveryId,
            cancellationToken);

    public Task AddAsync(Campaign campaign, CancellationToken cancellationToken = default)
    {
        context.Campaigns.Add(campaign);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}

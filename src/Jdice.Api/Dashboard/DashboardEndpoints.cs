using Jdice.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Jdice.Domain.Campaigns;

namespace Jdice.Api.Dashboard;

public sealed record DashboardResponse(
    int Modelos,
    int Destinatarios,
    int Listas,
    int DisparosAgendados,
    int EmailsEnviados,
    int EnviadosNosUltimos30Dias,
    IReadOnlyList<DisparoRecenteResponse> Recentes);

public sealed record DisparoRecenteResponse(
    Guid Id,
    string Nome,
    string Situacao,
    int Total,
    int Enviados,
    DateTimeOffset AgendadoPara);

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("/api/dashboard", GetAsync)
            .WithTags("Dashboard")
            .RequireAuthorization();

        return builder;
    }

    /// <summary>
    /// Números reais do sistema. O projeto original exibia "taxa de abertura
    /// ~68%" e "735 aberturas estimadas" escritos no código — números
    /// inventados que davam a impressão de um sistema que media coisas.
    /// </summary>
    private static async Task<Ok<DashboardResponse>> GetAsync(
        JdiceDbContext context,
        CancellationToken cancellationToken)
    {
        var trintaDiasAtras = DateTimeOffset.UtcNow.AddDays(-30);

        var recentes = await context.Campaigns
            .OrderByDescending(campaign => campaign.ScheduledFor)
            .Take(5)
            .Select(campaign => new
            {
                campaign.Id,
                campaign.Name,
                campaign.Status,
                campaign.ScheduledFor,
                Total = context.Deliveries.Count(d => d.CampaignId == campaign.Id),
                Enviados = context.Deliveries.Count(d =>
                    d.CampaignId == campaign.Id && d.Status == DeliveryStatus.Sent)
            })
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new DashboardResponse(
            await context.Templates.CountAsync(t => t.ArchivedAt == null, cancellationToken),
            await context.Recipients.CountAsync(r => r.UnsubscribedAt == null, cancellationToken),
            await context.RecipientLists.CountAsync(cancellationToken),
            await context.Campaigns.CountAsync(
                c => c.Status == CampaignStatus.Scheduled,
                cancellationToken),
            await context.Deliveries.CountAsync(
                d => d.Status == DeliveryStatus.Sent,
                cancellationToken),
            await context.Deliveries.CountAsync(
                d => d.Status == DeliveryStatus.Sent && d.SentAt >= trintaDiasAtras,
                cancellationToken),
            [.. recentes.Select(item => new DisparoRecenteResponse(
                item.Id,
                item.Name,
                item.Status.ToString(),
                item.Total,
                item.Enviados,
                item.ScheduledFor))]));
    }
}

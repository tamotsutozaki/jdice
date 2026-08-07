using Hangfire;
using Jdice.Application.Abstractions;
using Jdice.Application.Campaigns;
using Microsoft.Extensions.Logging;

namespace Jdice.Infrastructure.Scheduling;

/// <summary>
/// Agenda disparos no Hangfire, que guarda os jobs no próprio Postgres — então
/// um reinício do sistema não perde nada agendado.
/// <para>
/// Substitui o Quartz do projeto Java, com duas diferenças que importam: os
/// jobs guardam apenas o identificador da campanha, e não o objeto serializado
/// (mudar uma classe não invalida o que já está agendado), e existe rota para
/// cancelar — o que lá não havia.
/// </para>
/// </summary>
public sealed class HangfireCampaignScheduler(
    IBackgroundJobClient jobs,
    ILogger<HangfireCampaignScheduler> logger) : ICampaignScheduler
{
    public string Schedule(Guid campaignId, DateTimeOffset quando)
    {
        var agora = DateTimeOffset.UtcNow;

        // Data no passado — ou o "agora" de um disparo imediato — vira
        // execução na primeira oportunidade, em vez de ficar preso.
        return quando <= agora
            ? jobs.Enqueue<CampaignProcessor>(processor =>
                processor.ProcessAsync(campaignId, CancellationToken.None))
            : jobs.Schedule<CampaignProcessor>(
                processor => processor.ProcessAsync(campaignId, CancellationToken.None),
                quando);
    }

    public bool Cancel(string jobId)
    {
        var cancelado = jobs.Delete(jobId);

        if (!cancelado)
        {
            // Job já executado ou já removido. Quem pediu o cancelamento não
            // precisa tratar isso como falha.
            logger.LogInformation("Job {JobId} já não estava agendado.", jobId);
        }

        return cancelado;
    }
}

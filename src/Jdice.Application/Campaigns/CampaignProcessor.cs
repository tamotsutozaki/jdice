using Jdice.Application.Abstractions;
using Jdice.Domain.Campaigns;
using Microsoft.Extensions.Logging;

namespace Jdice.Application.Campaigns;

/// <summary>
/// Executa o disparo: renderiza o conteúdo para cada pessoa e envia. Roda no
/// worker, nunca no processo que atende HTTP.
/// <para>
/// Todo o cuidado aqui é contra enviar duas vezes. A entrega só é processada
/// se ninguém a tiver assumido antes, e o estado vai para o banco <em>antes</em>
/// da mensagem sair — se o processo morrer entre gravar e enviar, a pessoa
/// deixa de receber; se fosse ao contrário, receberia duas vezes. Entre os
/// dois erros, o primeiro é o menos grave.
/// </para>
/// </summary>
public sealed class CampaignProcessor(
    ICampaignRepository campaigns,
    IRecipientRepository recipients,
    ITemplateRepository templates,
    ITemplateRenderer renderer,
    IEmailSender emailSender,
    IDeliveryQueue queue,
    TimeProvider clock,
    ILogger<CampaignProcessor> logger)
{
    /// <summary>Quantas entregas buscar por vez, para não carregar milhares na memória.</summary>
    private const int TamanhoDoLote = 100;

    /// <summary>Folga aceita entre o relógio do worker e o de quem agendou.</summary>
    private static readonly TimeSpan ToleranciaDeAdiantamento = TimeSpan.FromMinutes(1);

    public async Task ProcessAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        var campaign = await campaigns.FindHeaderAsync(campaignId, cancellationToken);

        if (campaign is null)
        {
            logger.LogWarning("Disparo {Id} não encontrado; nada a fazer.", campaignId);
            return;
        }

        if (campaign.Status is CampaignStatus.Cancelled)
        {
            logger.LogInformation("Disparo {Id} foi cancelado; nada será enviado.", campaignId);
            return;
        }

        // Confere a hora aqui também, e não só no agendador. Quem garante o
        // horário é o Hangfire, mas um reprocessamento manual ou uma falha
        // dele mandaria a mensagem antes do combinado — e e-mail enviado cedo
        // não tem como voltar. A tolerância existe porque o relógio do worker
        // e o de quem agendou não são exatamente o mesmo.
        var agora = clock.GetUtcNow();

        if (campaign.ScheduledFor > agora.Add(ToleranciaDeAdiantamento))
        {
            logger.LogWarning(
                "Disparo {Id} está agendado para {Quando} e não deve sair agora.",
                campaignId,
                campaign.ScheduledFor);

            return;
        }

        // Se outro worker já assumiu, este desiste em vez de reprocessar.
        if (!campaign.TryStart(agora) && campaign.Status is not CampaignStatus.Running)
        {
            logger.LogInformation("Disparo {Id} já foi processado.", campaignId);
            return;
        }

        await campaigns.SaveChangesAsync(cancellationToken);

        var versao = await CarregarVersaoAsync(campaign, cancellationToken);

        if (versao is null)
        {
            logger.LogError(
                "Versão {VersaoId} do modelo sumiu; disparo {Id} não pode continuar.",
                campaign.TemplateVersionId,
                campaignId);

            campaign.Complete(clock.GetUtcNow());
            await campaigns.SaveChangesAsync(cancellationToken);

            return;
        }

        if (queue.IsEnabled)
        {
            // Com a fila ligada, este job só reparte o trabalho: cada entrega
            // vira uma mensagem e os workers consomem em paralelo. Sem ela,
            // acrescentar workers não adiantaria nada, porque um disparo
            // inteiro seria um job só percorrendo tudo em série.
            await RepartirNaFilaAsync(campaignId, cancellationToken);
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            // Aqui não precisa de cursor: processar muda a situação da
            // entrega, então a consulta seguinte traz as próximas.
            var lote = await campaigns.NextPendingAsync(
                campaignId,
                TamanhoDoLote,
                cancellationToken: cancellationToken);

            if (lote.Count == 0)
            {
                break;
            }

            foreach (var entrega in lote)
            {
                await ProcessarEntregaAsync(campaign, versao.Html, entrega, cancellationToken);
            }
        }

        var resumo = await campaigns.SummaryAsync(campaignId, cancellationToken);

        if (resumo.Terminou)
        {
            campaign.Complete(clock.GetUtcNow());
            await campaigns.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Disparo {Id} concluído: {Enviados} enviados, {Falhados} falharam, {Pulados} pulados.",
                campaignId,
                resumo.Enviados,
                resumo.Falhados,
                resumo.Pulados);
        }
    }

    /// <summary>Coloca cada entrega pendente na fila, em lotes.</summary>
    private async Task RepartirNaFilaAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var publicadas = 0;
        Guid? cursor = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            // Percorre por cursor, e não repetindo a mesma consulta: publicar
            // não muda a situação da entrega, então buscar "as primeiras
            // pendentes" traria sempre o mesmo lote e o resto nunca sairia.
            var lote = await campaigns.NextPendingAsync(
                campaignId,
                TamanhoDoLote,
                cursor,
                cancellationToken);

            if (lote.Count == 0)
            {
                break;
            }

            await queue.PublishAsync(
                [.. lote.Select(entrega => new DeliveryWork(campaignId, entrega.Id))],
                cancellationToken);

            publicadas += lote.Count;
            cursor = lote[^1].Id;
        }

        logger.LogInformation(
            "Disparo {Id}: {Total} entregas repartidas entre os workers.",
            campaignId,
            publicadas);
    }

    /// <summary>
    /// Processa uma entrega avulsa, vinda da fila. Recarrega a campanha porque
    /// o consumidor não tem o contexto de quem repartiu.
    /// </summary>
    public async Task ProcessDeliveryAsync(
        Guid campaignId,
        Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        var campaign = await campaigns.FindHeaderAsync(campaignId, cancellationToken);

        if (campaign is null || campaign.Status is CampaignStatus.Cancelled)
        {
            return;
        }

        var entrega = await campaigns.FindDeliveryAsync(deliveryId, cancellationToken);

        if (entrega is null)
        {
            return;
        }

        var versao = await CarregarVersaoAsync(campaign, cancellationToken);

        if (versao is null)
        {
            entrega.Skip("A versão do modelo não existe mais.");
            await campaigns.SaveChangesAsync(cancellationToken);
            return;
        }

        await ProcessarEntregaAsync(campaign, versao.Html, entrega, cancellationToken);

        // Quem processa a última entrega fecha o disparo. Com vários workers,
        // não há um "coordenador" para fazer isso no fim.
        var resumo = await campaigns.SummaryAsync(campaignId, cancellationToken);

        if (resumo.Terminou && campaign.Status is CampaignStatus.Running)
        {
            campaign.Complete(clock.GetUtcNow());
            await campaigns.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ProcessarEntregaAsync(
        Campaign campaign,
        string html,
        Delivery entrega,
        CancellationToken cancellationToken)
    {
        // Assume a entrega e grava antes de qualquer envio: é o que impede
        // dois workers de mandarem a mesma mensagem.
        if (!entrega.TryStart())
        {
            return;
        }

        await campaigns.SaveChangesAsync(cancellationToken);

        var destinatario = await recipients.FindByIdAsync(entrega.RecipientId, cancellationToken);

        if (destinatario is null)
        {
            entrega.Skip("Destinatário não existe mais.");
            await campaigns.SaveChangesAsync(cancellationToken);
            return;
        }

        // Conferido de novo aqui, e não só na montagem: entre criar o disparo
        // e ele sair pode ter passado tempo, e nesse meio a pessoa pode ter
        // pedido para não receber mais.
        if (!destinatario.IsSubscribed)
        {
            entrega.Skip("Destinatário se descadastrou depois que o disparo foi criado.");
            await campaigns.SaveChangesAsync(cancellationToken);
            return;
        }

        var valores = MontarValores(campaign, destinatario.Name, destinatario.Fields);
        var renderizado = renderer.Render(html, valores);

        if (!renderizado.Succeeded || renderizado.Html is null)
        {
            // Conteúdo que não renderiza não melhora com nova tentativa.
            entrega.Skip($"Não foi possível montar o e-mail: {PrimeiroErro(renderizado)}");
            await campaigns.SaveChangesAsync(cancellationToken);
            return;
        }

        var assunto = renderer.Render(campaign.Subject, valores);

        var resultado = await emailSender.SendAsync(
            new EmailMessage(
                destinatario.Email,
                destinatario.Name,
                assunto.Succeeded && assunto.Html is not null ? assunto.Html : campaign.Subject,
                renderizado.Html,
                campaign.FromName),
            cancellationToken);

        if (resultado.Enviado)
        {
            entrega.MarkSent(clock.GetUtcNow());
        }
        else if (resultado.Permanente)
        {
            // Recusa definitiva: insistir não muda o resultado e ainda
            // prejudica a reputação do remetente.
            entrega.Skip(resultado.Motivo);
        }
        else
        {
            entrega.MarkFailed(resultado.Motivo, clock.GetUtcNow());
        }

        await campaigns.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Junta os valores comuns do disparo com os campos da pessoa. Os campos
    /// dela têm precedência: é o que faz cada e-mail sair personalizado.
    /// </summary>
    private static Dictionary<string, string?> MontarValores(
        Campaign campaign,
        string nome,
        IReadOnlyDictionary<string, string> campos)
    {
        var valores = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (chave, valor) in campaign.SharedValues)
        {
            valores[chave] = valor;
        }

        foreach (var (chave, valor) in campos)
        {
            valores[chave] = valor;
        }

        // "nome" está sempre disponível, mesmo que a planilha não traga coluna
        // com esse rótulo.
        valores["nome"] = nome;

        return valores;
    }

    private async Task<Domain.Templates.TemplateVersion?> CarregarVersaoAsync(
        Campaign campaign,
        CancellationToken cancellationToken)
    {
        var template = await templates.FindByIdAsync(campaign.TemplateId, cancellationToken);

        return template?.Versions.FirstOrDefault(versao => versao.Id == campaign.TemplateVersionId);
    }

    private static string PrimeiroErro(TemplateRenderResult resultado) =>
        resultado.Errors.Count > 0 ? resultado.Errors[0].Message : "erro desconhecido";
}

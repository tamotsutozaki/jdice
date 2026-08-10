using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using Jdice.Api.Auth;
using Jdice.Api.Recipients;
using Jdice.Application.Abstractions;
using Jdice.Application.Campaigns;
using Jdice.Application.Templates;
using Jdice.Domain.Campaigns;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Jdice.Api.Campaigns;

public sealed record CreateCampaignRequest(
    string? Nome,
    [property: Required] Guid TemplateId,
    Guid? TemplateVersionId,
    [property: Required, MaxLength(Campaign.MaximumSubjectLength)] string Assunto,
    string? Remetente,
    IReadOnlyDictionary<string, string>? ValoresComuns,
    IReadOnlyList<Guid>? ListaIds,
    IReadOnlyList<Guid>? DestinatarioIds,
    /// <summary>Quando enviar. Ausente ou no passado significa agora.</summary>
    DateTimeOffset? Agendar,
    string? FusoHorario);

public sealed record RescheduleRequest(
    [property: Required] DateTimeOffset Agendar,
    string? FusoHorario);

public sealed record CampaignResponse(
    Guid Id,
    string Nome,
    string Assunto,
    Guid TemplateId,
    int VersaoDoModelo,
    string Situacao,
    DateTimeOffset AgendadoPara,
    string FusoHorario,
    DateTimeOffset CriadoEm,
    DateTimeOffset? IniciadoEm,
    DateTimeOffset? ConcluidoEm,
    DeliverySummaryResponse Resumo);

public sealed record DeliverySummaryResponse(
    int Total,
    int Pendentes,
    int Enviando,
    int Enviados,
    int Falhados,
    int Pulados);

public sealed record DeliveryResponse(
    Guid Id,
    string Email,
    string Situacao,
    int Tentativas,
    string Erro,
    DateTimeOffset? EnviadoEm,
    IReadOnlyList<TentativaResponse> Historico);

public sealed record TentativaResponse(int Numero, DateTimeOffset Quando, string Erro);

public static class CampaignEndpoints
{
    public static IEndpointRouteBuilder MapCampaignEndpoints(this IEndpointRouteBuilder builder)
    {
        var grupo = builder.MapGroup("/api/campaigns")
            .WithTags("Campaigns")
            .RequireAuthorization();

        grupo.MapGet("/", ListAsync);
        grupo.MapGet("/{id:guid}", GetAsync);
        grupo.MapGet("/{id:guid}/deliveries", DeliveriesAsync);
        grupo.MapGet("/{id:guid}/deliveries/export", ExportDeliveriesAsync);
        grupo.MapPost("/", CreateAsync);
        grupo.MapPost("/{id:guid}/cancel", CancelAsync);
        grupo.MapPost("/{id:guid}/reschedule", RescheduleAsync);

        return builder;
    }

    private static async Task<Ok<PagedResponse<CampaignResponse>>> ListAsync(
        CampaignService campaigns,
        CancellationToken cancellationToken,
        string? situacao = null,
        int pagina = 1,
        int tamanho = 25)
    {
        CampaignStatus? filtro = Enum.TryParse<CampaignStatus>(situacao, true, out var valor)
            ? valor
            : null;

        var resultado = await campaigns.ListAsync(
            new CampaignFilter(filtro, pagina, tamanho),
            cancellationToken);

        var itens = new List<CampaignResponse>();

        foreach (var campaign in resultado.Itens)
        {
            itens.Add(Para(campaign, await campaigns.SummaryAsync(campaign.Id, cancellationToken)));
        }

        return TypedResults.Ok(new PagedResponse<CampaignResponse>(
            itens,
            resultado.Total,
            resultado.Pagina,
            resultado.TamanhoDaPagina,
            resultado.TotalDePaginas));
    }

    private static async Task<Results<Ok<CampaignResponse>, NotFound<ProblemDetails>>> GetAsync(
        Guid id,
        CampaignService campaigns,
        CancellationToken cancellationToken)
    {
        try
        {
            var campaign = await campaigns.GetAsync(id, cancellationToken);
            var resumo = await campaigns.SummaryAsync(id, cancellationToken);

            return TypedResults.Ok(Para(campaign, resumo));
        }
        catch (CampaignNotFoundException excecao)
        {
            return TypedResults.NotFound(NaoEncontrado(excecao.Message));
        }
    }

    private static async Task<Ok<IReadOnlyList<DeliveryResponse>>> DeliveriesAsync(
        Guid id,
        CampaignService campaigns,
        CancellationToken cancellationToken,
        int limite = 200)
    {
        var entregas = await campaigns.DeliveriesAsync(id, limite, cancellationToken);

        IReadOnlyList<DeliveryResponse> resposta = [.. entregas.Select(ParaEntrega)];

        return TypedResults.Ok(resposta);
    }

    /// <summary>
    /// Exporta todas as entregas em CSV. Sem teto de 200 como a listagem: um
    /// export pela metade não serve para conferência. Vai em streaming para não
    /// carregar o disparo inteiro na memória.
    /// </summary>
    private static async Task<IResult> ExportDeliveriesAsync(
        Guid id,
        CampaignService campaigns,
        CancellationToken cancellationToken)
    {
        var conteudo = new StringBuilder();
        conteudo.Append("email;situacao;tentativas;erro;enviado_em\n");

        await foreach (var entrega in campaigns.StreamDeliveriesAsync(id, cancellationToken))
        {
            conteudo
                .Append(Csv(entrega.Email)).Append(';')
                .Append(entrega.Status).Append(';')
                .Append(entrega.Attempts).Append(';')
                .Append(Csv(entrega.Error)).Append(';')
                .Append(entrega.SentAt?.ToString("o") ?? string.Empty).Append('\n');
        }

        // BOM na frente para o Excel abrir os acentos em UTF-8 sem embaralhar.
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(conteudo.ToString());

        return Results.File(bytes, "text/csv; charset=utf-8", $"disparo-{id}.csv");
    }

    /// <summary>Escapa um campo de CSV: aspas quando houver separador, aspas ou quebra de linha.</summary>
    private static string Csv(string valor)
    {
        if (valor.IndexOfAny([';', '"', '\n', '\r']) < 0)
        {
            return valor;
        }

        return $"\"{valor.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static DeliveryResponse ParaEntrega(Delivery entrega) => new(
        entrega.Id,
        entrega.Email,
        entrega.Status.ToString(),
        entrega.Attempts,
        entrega.Error,
        entrega.SentAt,
        [.. entrega.AttemptLog.Select(t => new TentativaResponse(t.Numero, t.Quando, t.Erro))]);

    private static async Task<Results<
        Accepted<CampaignResponse>,
        NotFound<ProblemDetails>,
        BadRequest<ProblemDetails>,
        Conflict<ProblemDetails>>> CreateAsync(
        [FromBody] CreateCampaignRequest request,
        ClaimsPrincipal principal,
        CampaignService campaigns,
        CancellationToken cancellationToken)
    {
        try
        {
            var campaign = await campaigns.CreateAsync(
                new CampaignRequest(
                    request.Nome ?? string.Empty,
                    request.TemplateId,
                    request.TemplateVersionId,
                    request.Assunto,
                    request.Remetente,
                    request.ValoresComuns,
                    request.ListaIds ?? [],
                    request.DestinatarioIds ?? [],
                    request.Agendar,
                    request.FusoHorario),
                principal.UserId() ?? Guid.Empty,
                cancellationToken);

            var resumo = await campaigns.SummaryAsync(campaign.Id, cancellationToken);

            // 202, não 201: o trabalho foi aceito e será feito pelo worker. A
            // requisição não espera o envio — com milhares de destinatários,
            // esperar seria segurar a conexão por minutos.
            return TypedResults.Accepted(
                $"/api/campaigns/{campaign.Id}",
                Para(campaign, resumo));
        }
        catch (TemplateNotFoundException excecao)
        {
            return TypedResults.NotFound(NaoEncontrado(excecao.Message));
        }
        catch (NoRecipientsException excecao)
        {
            return TypedResults.BadRequest(Invalido(excecao.Message));
        }
        catch (CampaignStateException excecao)
        {
            return TypedResults.Conflict(Conflito(excecao.Message));
        }
        catch (ArgumentException excecao)
        {
            return TypedResults.BadRequest(Invalido(excecao.Message));
        }
    }

    private static async Task<Results<
        NoContent,
        NotFound<ProblemDetails>,
        Conflict<ProblemDetails>>> CancelAsync(
        Guid id,
        CampaignService campaigns,
        CancellationToken cancellationToken)
    {
        try
        {
            await campaigns.CancelAsync(id, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (CampaignNotFoundException excecao)
        {
            return TypedResults.NotFound(NaoEncontrado(excecao.Message));
        }
        catch (CampaignStateException excecao)
        {
            // Depois que a primeira mensagem saiu, dizer que "cancelou" seria
            // mentir para quem operou.
            return TypedResults.Conflict(Conflito(excecao.Message));
        }
    }

    private static async Task<Results<
        NoContent,
        NotFound<ProblemDetails>,
        Conflict<ProblemDetails>>> RescheduleAsync(
        Guid id,
        [FromBody] RescheduleRequest request,
        CampaignService campaigns,
        CancellationToken cancellationToken)
    {
        try
        {
            await campaigns.RescheduleAsync(id, request.Agendar, request.FusoHorario, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (CampaignNotFoundException excecao)
        {
            return TypedResults.NotFound(NaoEncontrado(excecao.Message));
        }
        catch (CampaignStateException excecao)
        {
            return TypedResults.Conflict(Conflito(excecao.Message));
        }
    }

    private static CampaignResponse Para(Campaign campaign, DeliverySummary resumo) => new(
        campaign.Id,
        campaign.Name,
        campaign.Subject,
        campaign.TemplateId,
        campaign.TemplateVersionNumber,
        campaign.Status.ToString(),
        campaign.ScheduledFor,
        campaign.TimeZone,
        campaign.CreatedAt,
        campaign.StartedAt,
        campaign.CompletedAt,
        new DeliverySummaryResponse(
            resumo.Total,
            resumo.Pendentes,
            resumo.Enviando,
            resumo.Enviados,
            resumo.Falhados,
            resumo.Pulados));

    private static ProblemDetails NaoEncontrado(string detalhe) => new()
    {
        Status = StatusCodes.Status404NotFound,
        Title = "Disparo não encontrado",
        Detail = detalhe
    };

    private static ProblemDetails Conflito(string detalhe) => new()
    {
        Status = StatusCodes.Status409Conflict,
        Title = "Operação não permitida",
        Detail = detalhe
    };

    private static ProblemDetails Invalido(string detalhe) => new()
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "Requisição inválida",
        Detail = detalhe
    };
}

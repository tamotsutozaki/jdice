using System.Security.Claims;
using Jdice.Api.Auth;
using Jdice.Application.Abstractions;
using Jdice.Application.Templates;
using Jdice.Domain.Templates;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Jdice.Api.Templates;

public static class TemplateEndpoints
{
    public static IEndpointRouteBuilder MapTemplateEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api/templates")
            .WithTags("Templates")
            .RequireAuthorization();

        group.MapGet("/", ListAsync);
        group.MapGet("/categories", ListCategoriesAsync);
        group.MapGet("/tags", ListTagsAsync);
        group.MapGet("/{id:guid}", GetAsync);

        // Criar modelo e criar versão são o trabalho diário e ficam abertos a
        // qualquer usuário: como nada pode ser alterado nem perdido, o pior
        // caso é uma versão a mais.
        group.MapPost("/", CreateAsync);
        group.MapPost("/{id:guid}/versions", AddVersionAsync);
        group.MapPut("/{id:guid}", UpdateDetailsAsync);

        group.MapPost("/preview", Preview);
        group.MapPost("/analyze", Analyze);

        // Tirar de circulação afeta o trabalho de todos: fica com Admin.
        group.MapDelete("/{id:guid}", ArchiveAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);
        group.MapPost("/{id:guid}/unarchive", UnarchiveAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return builder;
    }

    private static async Task<Ok<IReadOnlyList<TemplateListItemResponse>>> ListAsync(
        TemplateService templates,
        CancellationToken cancellationToken,
        string? busca = null,
        string? categoria = null,
        string? tag = null,
        bool incluirArquivados = false)
    {
        var encontrados = await templates.ListAsync(
            new TemplateFilter(busca, categoria, tag, incluirArquivados),
            cancellationToken);

        IReadOnlyList<TemplateListItemResponse> resposta =
            [.. encontrados.Select(ParaItemDeLista)];

        return TypedResults.Ok(resposta);
    }

    private static async Task<Ok<IReadOnlyList<string>>> ListCategoriesAsync(
        TemplateService templates,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await templates.ListCategoriesAsync(cancellationToken));

    private static async Task<Ok<IReadOnlyList<string>>> ListTagsAsync(
        TemplateService templates,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await templates.ListTagsAsync(cancellationToken));

    private static async Task<Results<Ok<TemplateDetailResponse>, NotFound<ProblemDetails>>> GetAsync(
        Guid id,
        TemplateService templates,
        CancellationToken cancellationToken)
    {
        try
        {
            return TypedResults.Ok(ParaDetalhe(await templates.GetAsync(id, cancellationToken)));
        }
        catch (TemplateNotFoundException exception)
        {
            return TypedResults.NotFound(NaoEncontrado(exception.Message));
        }
    }

    private static async Task<Results<
        Created<TemplateDetailResponse>,
        Conflict<ProblemDetails>,
        BadRequest<ProblemDetails>>> CreateAsync(
        [FromBody] CreateTemplateRequest request,
        ClaimsPrincipal principal,
        TemplateService templates,
        CancellationToken cancellationToken)
    {
        try
        {
            var template = await templates.CreateAsync(
                request.Nome,
                request.Categoria ?? string.Empty,
                request.Tags,
                request.Html,
                principal.UserId() ?? Guid.Empty,
                cancellationToken);

            return TypedResults.Created($"/api/templates/{template.Id}", ParaDetalhe(template));
        }
        catch (TemplateNameAlreadyInUseException exception)
        {
            return TypedResults.Conflict(Conflito("Nome já usado", exception.Message));
        }
        catch (InvalidTemplateContentException exception)
        {
            return TypedResults.BadRequest(ConteudoInvalido(exception));
        }
        catch (ArgumentException exception)
        {
            return TypedResults.BadRequest(RequisicaoInvalida(exception.Message));
        }
    }

    private static async Task<Results<
        Created<TemplateVersionResponse>,
        NotFound<ProblemDetails>,
        Conflict<ProblemDetails>,
        BadRequest<ProblemDetails>>> AddVersionAsync(
        Guid id,
        [FromBody] CreateVersionRequest request,
        ClaimsPrincipal principal,
        TemplateService templates,
        CancellationToken cancellationToken)
    {
        try
        {
            var version = await templates.AddVersionAsync(
                id,
                request.Html,
                principal.UserId() ?? Guid.Empty,
                cancellationToken);

            return TypedResults.Created(
                $"/api/templates/{id}",
                ParaVersao(version));
        }
        catch (TemplateNotFoundException exception)
        {
            return TypedResults.NotFound(NaoEncontrado(exception.Message));
        }
        catch (ArchivedTemplateException exception)
        {
            return TypedResults.Conflict(Conflito("Modelo arquivado", exception.Message));
        }
        catch (InvalidTemplateContentException exception)
        {
            return TypedResults.BadRequest(ConteudoInvalido(exception));
        }
    }

    private static async Task<Results<
        Ok<TemplateDetailResponse>,
        NotFound<ProblemDetails>,
        Conflict<ProblemDetails>,
        BadRequest<ProblemDetails>>> UpdateDetailsAsync(
        Guid id,
        [FromBody] UpdateTemplateRequest request,
        TemplateService templates,
        CancellationToken cancellationToken)
    {
        try
        {
            var template = await templates.UpdateDetailsAsync(
                id,
                request.Nome,
                request.Categoria ?? string.Empty,
                request.Tags,
                cancellationToken);

            return TypedResults.Ok(ParaDetalhe(template));
        }
        catch (TemplateNotFoundException exception)
        {
            return TypedResults.NotFound(NaoEncontrado(exception.Message));
        }
        catch (TemplateNameAlreadyInUseException exception)
        {
            return TypedResults.Conflict(Conflito("Nome já usado", exception.Message));
        }
        catch (ArgumentException exception)
        {
            return TypedResults.BadRequest(RequisicaoInvalida(exception.Message));
        }
    }

    private static Ok<PreviewResponse> Preview(
        [FromBody] PreviewRequest request,
        TemplateService templates)
    {
        var resultado = templates.Preview(
            request.Html,
            request.Valores ?? new Dictionary<string, string?>());

        // Erro de conteúdo não é falha da requisição: quem está escrevendo o
        // modelo precisa ver o problema junto do preview, não um 400 seco.
        return TypedResults.Ok(new PreviewResponse(
            resultado.Html,
            [.. resultado.Errors.Select(Descrever)]));
    }

    private static Ok<AnalysisResponse> Analyze(
        [FromBody] PreviewRequest request,
        TemplateService templates)
    {
        var analise = templates.Analyze(request.Html);

        return TypedResults.Ok(new AnalysisResponse(
            analise.Variables,
            [.. analise.Errors.Select(Descrever)]));
    }

    private static async Task<Results<NoContent, NotFound<ProblemDetails>>> ArchiveAsync(
        Guid id,
        TemplateService templates,
        CancellationToken cancellationToken)
    {
        try
        {
            await templates.ArchiveAsync(id, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (TemplateNotFoundException exception)
        {
            return TypedResults.NotFound(NaoEncontrado(exception.Message));
        }
    }

    private static async Task<Results<NoContent, NotFound<ProblemDetails>>> UnarchiveAsync(
        Guid id,
        TemplateService templates,
        CancellationToken cancellationToken)
    {
        try
        {
            await templates.UnarchiveAsync(id, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (TemplateNotFoundException exception)
        {
            return TypedResults.NotFound(NaoEncontrado(exception.Message));
        }
    }

    private static TemplateListItemResponse ParaItemDeLista(Template template) => new(
        template.Id,
        template.Name,
        template.Category,
        template.Tags,
        template.Versions.Count,
        template.CurrentVersion?.Number,
        template.CurrentVersion?.Variables ?? [],
        template.IsArchived,
        template.UpdatedAt);

    private static TemplateDetailResponse ParaDetalhe(Template template) => new(
        template.Id,
        template.Name,
        template.Category,
        template.Tags,
        template.IsArchived,
        template.CreatedBy,
        template.CreatedAt,
        template.UpdatedAt,
        [.. template.Versions.OrderByDescending(version => version.Number).Select(ParaVersao)]);

    private static TemplateVersionResponse ParaVersao(TemplateVersion version) => new(
        version.Id,
        version.Number,
        version.Html,
        version.Variables,
        version.CreatedBy,
        version.CreatedAt);

    private static string Descrever(TemplateError erro) =>
        erro.Line is null ? erro.Message : $"Linha {erro.Line}: {erro.Message}";

    private static ProblemDetails NaoEncontrado(string detalhe) => new()
    {
        Status = StatusCodes.Status404NotFound,
        Title = "Modelo não encontrado",
        Detail = detalhe
    };

    private static ProblemDetails Conflito(string titulo, string detalhe) => new()
    {
        Status = StatusCodes.Status409Conflict,
        Title = titulo,
        Detail = detalhe
    };

    private static ProblemDetails RequisicaoInvalida(string detalhe) => new()
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "Requisição inválida",
        Detail = detalhe
    };

    private static ProblemDetails ConteudoInvalido(InvalidTemplateContentException exception) => new()
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "Conteúdo do modelo inválido",
        Detail = string.Join(" ", exception.Erros),
        Extensions = { ["erros"] = exception.Erros }
    };
}

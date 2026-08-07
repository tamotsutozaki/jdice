using System.Security.Claims;
using Jdice.Api.Auth;
using Jdice.Application.Abstractions;
using Jdice.Application.Recipients;
using Jdice.Domain.Recipients;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Jdice.Api.Recipients;

public static class RecipientEndpoints
{
    /// <summary>Teto do arquivo de importação: 10 MB comportam bem mais que as 50 mil linhas aceitas.</summary>
    private const long TamanhoMaximoDoArquivo = 10 * 1024 * 1024;

    public static IEndpointRouteBuilder MapRecipientEndpoints(this IEndpointRouteBuilder builder)
    {
        var destinatarios = builder.MapGroup("/api/recipients")
            .WithTags("Recipients")
            .RequireAuthorization();

        destinatarios.MapGet("/", ListAsync);
        destinatarios.MapGet("/{id:guid}", GetAsync);
        destinatarios.MapGet("/{id:guid}/lists", ListsOfAsync);
        destinatarios.MapPost("/", CreateAsync);
        destinatarios.MapPut("/{id:guid}", UpdateAsync);
        destinatarios.MapPost("/{id:guid}/unsubscribe", UnsubscribeAsync);
        destinatarios.MapPost("/{id:guid}/resubscribe", ResubscribeAsync);

        // Remover de vez apaga o histórico da pessoa; fica com Admin.
        destinatarios.MapDelete("/{id:guid}", DeleteAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        destinatarios.MapPost("/import", ImportAsync)
            .DisableAntiforgery();

        var listas = builder.MapGroup("/api/recipient-lists")
            .WithTags("Recipients")
            .RequireAuthorization();

        listas.MapGet("/", ListListsAsync);
        listas.MapGet("/{id:guid}", GetListAsync);
        listas.MapPost("/", CreateListAsync);
        listas.MapPut("/{id:guid}", UpdateListAsync);
        listas.MapDelete("/{id:guid}", DeleteListAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        listas.MapPost("/{id:guid}/members/{recipientId:guid}", AddMemberAsync);
        listas.MapDelete("/{id:guid}/members/{recipientId:guid}", RemoveMemberAsync);

        return builder;
    }

    private static async Task<Ok<PagedResponse<RecipientResponse>>> ListAsync(
        RecipientService recipients,
        CancellationToken cancellationToken,
        Guid? listaId = null,
        string? busca = null,
        bool incluirDescadastrados = false,
        int pagina = 1,
        int tamanho = 50)
    {
        var resultado = await recipients.ListAsync(
            new RecipientFilter(listaId, busca, incluirDescadastrados, pagina, tamanho),
            cancellationToken);

        return TypedResults.Ok(new PagedResponse<RecipientResponse>(
            [.. resultado.Itens.Select(Para)],
            resultado.Total,
            resultado.Pagina,
            resultado.TamanhoDaPagina,
            resultado.TotalDePaginas));
    }

    private static async Task<Results<Ok<RecipientResponse>, NotFound<ProblemDetails>>> GetAsync(
        Guid id,
        RecipientService recipients,
        CancellationToken cancellationToken)
    {
        try
        {
            return TypedResults.Ok(Para(await recipients.GetAsync(id, cancellationToken)));
        }
        catch (RecipientNotFoundException excecao)
        {
            return TypedResults.NotFound(NaoEncontrado(excecao.Message));
        }
    }

    private static async Task<Ok<IReadOnlyList<RecipientListResponse>>> ListsOfAsync(
        Guid id,
        RecipientService recipients,
        CancellationToken cancellationToken)
    {
        var listas = await recipients.ListsOfAsync(id, cancellationToken);

        IReadOnlyList<RecipientListResponse> resposta =
            [.. listas.Select(lista => ParaLista(lista, 0, 0))];

        return TypedResults.Ok(resposta);
    }

    private static async Task<Results<
        Created<RecipientResponse>,
        Conflict<ProblemDetails>,
        NotFound<ProblemDetails>,
        BadRequest<ProblemDetails>>> CreateAsync(
        [FromBody] CreateRecipientRequest request,
        RecipientService recipients,
        CancellationToken cancellationToken)
    {
        try
        {
            var recipient = await recipients.CreateAsync(
                request.Email,
                request.Nome,
                request.Campos,
                request.ListaId,
                cancellationToken);

            return TypedResults.Created($"/api/recipients/{recipient.Id}", Para(recipient));
        }
        catch (RecipientEmailAlreadyInUseException excecao)
        {
            return TypedResults.Conflict(Conflito("E-mail já cadastrado", excecao.Message));
        }
        catch (RecipientListNotFoundException excecao)
        {
            return TypedResults.NotFound(NaoEncontrado(excecao.Message));
        }
        catch (ArgumentException excecao)
        {
            return TypedResults.BadRequest(Invalido(excecao.Message));
        }
    }

    private static async Task<Results<Ok<RecipientResponse>, NotFound<ProblemDetails>>> UpdateAsync(
        Guid id,
        [FromBody] UpdateRecipientRequest request,
        RecipientService recipients,
        CancellationToken cancellationToken)
    {
        try
        {
            var recipient = await recipients.UpdateAsync(
                id,
                request.Nome,
                request.Campos,
                cancellationToken);

            return TypedResults.Ok(Para(recipient));
        }
        catch (RecipientNotFoundException excecao)
        {
            return TypedResults.NotFound(NaoEncontrado(excecao.Message));
        }
    }

    private static async Task<Results<NoContent, NotFound<ProblemDetails>>> UnsubscribeAsync(
        Guid id,
        RecipientService recipients,
        CancellationToken cancellationToken)
    {
        try
        {
            await recipients.UnsubscribeAsync(id, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (RecipientNotFoundException excecao)
        {
            return TypedResults.NotFound(NaoEncontrado(excecao.Message));
        }
    }

    private static async Task<Results<NoContent, NotFound<ProblemDetails>>> ResubscribeAsync(
        Guid id,
        RecipientService recipients,
        CancellationToken cancellationToken)
    {
        try
        {
            await recipients.ResubscribeAsync(id, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (RecipientNotFoundException excecao)
        {
            return TypedResults.NotFound(NaoEncontrado(excecao.Message));
        }
    }

    private static async Task<Results<NoContent, NotFound<ProblemDetails>>> DeleteAsync(
        Guid id,
        RecipientService recipients,
        CancellationToken cancellationToken)
    {
        try
        {
            await recipients.DeleteAsync(id, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (RecipientNotFoundException excecao)
        {
            return TypedResults.NotFound(NaoEncontrado(excecao.Message));
        }
    }

    private static async Task<Results<
        Ok<ImportResultResponse>,
        NotFound<ProblemDetails>,
        BadRequest<ProblemDetails>>> ImportAsync(
        IFormFile arquivo,
        RecipientService recipients,
        CancellationToken cancellationToken,
        Guid? listaId = null)
    {
        if (arquivo is null || arquivo.Length == 0)
        {
            return TypedResults.BadRequest(Invalido("Envie um arquivo CSV."));
        }

        if (arquivo.Length > TamanhoMaximoDoArquivo)
        {
            return TypedResults.BadRequest(Invalido(
                $"O arquivo passa de {TamanhoMaximoDoArquivo / (1024 * 1024)} MB."));
        }

        try
        {
            await using var conteudo = arquivo.OpenReadStream();

            var resultado = await recipients.ImportAsync(conteudo, listaId, cancellationToken);

            // Sempre 200, mesmo com linhas recusadas: o arquivo foi processado
            // e o relatório é a resposta. Devolver erro faria a interface
            // descartar justamente a informação de que a pessoa precisa.
            return TypedResults.Ok(new ImportResultResponse(
                resultado.TotalDeLinhas,
                resultado.Importados,
                resultado.Criados,
                resultado.Atualizados,
                resultado.JaNaLista,
                [.. resultado.Recusados.Select(item => new ImportIssueResponse(item.Linha, item.Motivo))],
                resultado.ColunasLivres));
        }
        catch (RecipientListNotFoundException excecao)
        {
            return TypedResults.NotFound(NaoEncontrado(excecao.Message));
        }
    }

    private static async Task<Ok<IReadOnlyList<RecipientListResponse>>> ListListsAsync(
        RecipientListService listas,
        CancellationToken cancellationToken)
    {
        var encontradas = await listas.ListAsync(cancellationToken);

        IReadOnlyList<RecipientListResponse> resposta =
            [.. encontradas.Select(item => ParaLista(item.Lista, item.TotalDeMembros, item.TotalAtivos))];

        return TypedResults.Ok(resposta);
    }

    private static async Task<Results<Ok<RecipientListResponse>, NotFound<ProblemDetails>>> GetListAsync(
        Guid id,
        RecipientListService listas,
        CancellationToken cancellationToken)
    {
        try
        {
            var lista = await listas.GetAsync(id, cancellationToken);

            return TypedResults.Ok(ParaLista(lista, lista.Members.Count, lista.Members.Count));
        }
        catch (RecipientListNotFoundException excecao)
        {
            return TypedResults.NotFound(NaoEncontrado(excecao.Message));
        }
    }

    private static async Task<Results<
        Created<RecipientListResponse>,
        Conflict<ProblemDetails>,
        BadRequest<ProblemDetails>>> CreateListAsync(
        [FromBody] CreateRecipientListRequest request,
        ClaimsPrincipal principal,
        RecipientListService listas,
        CancellationToken cancellationToken)
    {
        try
        {
            var lista = await listas.CreateAsync(
                request.Nome,
                request.Descricao,
                principal.UserId() ?? Guid.Empty,
                cancellationToken);

            return TypedResults.Created(
                $"/api/recipient-lists/{lista.Id}",
                ParaLista(lista, 0, 0));
        }
        catch (RecipientListNameAlreadyInUseException excecao)
        {
            return TypedResults.Conflict(Conflito("Nome já usado", excecao.Message));
        }
        catch (ArgumentException excecao)
        {
            return TypedResults.BadRequest(Invalido(excecao.Message));
        }
    }

    private static async Task<Results<
        Ok<RecipientListResponse>,
        NotFound<ProblemDetails>,
        Conflict<ProblemDetails>>> UpdateListAsync(
        Guid id,
        [FromBody] CreateRecipientListRequest request,
        RecipientListService listas,
        CancellationToken cancellationToken)
    {
        try
        {
            var lista = await listas.UpdateAsync(id, request.Nome, request.Descricao, cancellationToken);

            return TypedResults.Ok(ParaLista(lista, lista.Members.Count, lista.Members.Count));
        }
        catch (RecipientListNotFoundException excecao)
        {
            return TypedResults.NotFound(NaoEncontrado(excecao.Message));
        }
        catch (RecipientListNameAlreadyInUseException excecao)
        {
            return TypedResults.Conflict(Conflito("Nome já usado", excecao.Message));
        }
    }

    private static async Task<Results<NoContent, NotFound<ProblemDetails>>> DeleteListAsync(
        Guid id,
        RecipientListService listas,
        CancellationToken cancellationToken)
    {
        try
        {
            await listas.DeleteAsync(id, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (RecipientListNotFoundException excecao)
        {
            return TypedResults.NotFound(NaoEncontrado(excecao.Message));
        }
    }

    private static async Task<Results<NoContent, NotFound<ProblemDetails>>> AddMemberAsync(
        Guid id,
        Guid recipientId,
        RecipientListService listas,
        CancellationToken cancellationToken)
    {
        try
        {
            await listas.AddMemberAsync(id, recipientId, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (Exception excecao)
            when (excecao is RecipientListNotFoundException or RecipientNotFoundException)
        {
            return TypedResults.NotFound(NaoEncontrado(excecao.Message));
        }
    }

    private static async Task<Results<NoContent, NotFound<ProblemDetails>>> RemoveMemberAsync(
        Guid id,
        Guid recipientId,
        RecipientListService listas,
        CancellationToken cancellationToken)
    {
        try
        {
            await listas.RemoveMemberAsync(id, recipientId, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (RecipientListNotFoundException excecao)
        {
            return TypedResults.NotFound(NaoEncontrado(excecao.Message));
        }
    }

    private static RecipientResponse Para(Recipient recipient) => new(
        recipient.Id,
        recipient.Email,
        recipient.Name,
        recipient.Fields,
        recipient.IsSubscribed,
        recipient.UnsubscribedAt,
        recipient.CreatedAt);

    private static RecipientListResponse ParaLista(
        RecipientList lista,
        int totalDeMembros,
        int totalAtivos) => new(
        lista.Id,
        lista.Name,
        lista.Description,
        totalDeMembros,
        totalAtivos,
        lista.CreatedAt,
        lista.UpdatedAt);

    private static ProblemDetails NaoEncontrado(string detalhe) => new()
    {
        Status = StatusCodes.Status404NotFound,
        Title = "Não encontrado",
        Detail = detalhe
    };

    private static ProblemDetails Conflito(string titulo, string detalhe) => new()
    {
        Status = StatusCodes.Status409Conflict,
        Title = titulo,
        Detail = detalhe
    };

    private static ProblemDetails Invalido(string detalhe) => new()
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "Requisição inválida",
        Detail = detalhe
    };
}

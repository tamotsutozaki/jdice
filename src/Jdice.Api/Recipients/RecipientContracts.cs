using System.ComponentModel.DataAnnotations;
using Jdice.Domain.Recipients;

namespace Jdice.Api.Recipients;

public sealed record CreateRecipientRequest(
    [property: Required, EmailAddress, MaxLength(320)] string Email,
    [property: MaxLength(Recipient.MaximumNameLength)] string? Nome,
    IReadOnlyDictionary<string, string>? Campos,
    /// <summary>Quando informada, já inclui o destinatário nesta lista.</summary>
    Guid? ListaId);

public sealed record UpdateRecipientRequest(
    [property: MaxLength(Recipient.MaximumNameLength)] string? Nome,
    IReadOnlyDictionary<string, string>? Campos);

public sealed record CreateRecipientListRequest(
    [property: Required, MaxLength(RecipientList.MaximumNameLength)] string Nome,
    [property: MaxLength(RecipientList.MaximumDescriptionLength)] string? Descricao);

public sealed record RecipientResponse(
    Guid Id,
    string Email,
    string Nome,
    IReadOnlyDictionary<string, string> Campos,
    bool Inscrito,
    DateTimeOffset? DescadastradoEm,
    DateTimeOffset CriadoEm);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Itens,
    int Total,
    int Pagina,
    int TamanhoDaPagina,
    int TotalDePaginas);

public sealed record RecipientListResponse(
    Guid Id,
    string Nome,
    string Descricao,
    int TotalDeMembros,
    int TotalAtivos,
    DateTimeOffset CriadoEm,
    DateTimeOffset AtualizadoEm);

public sealed record ImportIssueResponse(int Linha, string Motivo);

/// <summary>
/// O que aconteceu com o arquivo. Detalhado de propósito: importar mil linhas
/// e receber só "ok" não permite descobrir por que faltaram quatro.
/// </summary>
public sealed record ImportResultResponse(
    int TotalDeLinhas,
    int Importados,
    int Criados,
    int Atualizados,
    int JaNaLista,
    IReadOnlyList<ImportIssueResponse> Recusados,
    IReadOnlyList<string> ColunasLivres);

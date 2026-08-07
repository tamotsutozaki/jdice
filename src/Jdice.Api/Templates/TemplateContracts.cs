using System.ComponentModel.DataAnnotations;
using Jdice.Domain.Templates;

namespace Jdice.Api.Templates;

public sealed record CreateTemplateRequest(
    [property: Required, MaxLength(Template.MaximumNameLength)] string Nome,
    [property: MaxLength(Template.MaximumCategoryLength)] string? Categoria,
    IReadOnlyList<string>? Tags,
    [property: Required] string Html);

public sealed record UpdateTemplateRequest(
    [property: Required, MaxLength(Template.MaximumNameLength)] string Nome,
    [property: MaxLength(Template.MaximumCategoryLength)] string? Categoria,
    IReadOnlyList<string>? Tags);

public sealed record CreateVersionRequest([property: Required] string Html);

/// <param name="Valores">Valores de exemplo para as variáveis do modelo.</param>
public sealed record PreviewRequest(
    [property: Required] string Html,
    IReadOnlyDictionary<string, string?>? Valores);

public sealed record PreviewResponse(string? Html, IReadOnlyList<string> Erros);

public sealed record AnalysisResponse(IReadOnlyList<string> Variaveis, IReadOnlyList<string> Erros);

public sealed record TemplateVersionResponse(
    Guid Id,
    int Numero,
    string Html,
    IReadOnlyList<string> Variaveis,
    Guid CriadoPor,
    DateTimeOffset CriadoEm);

/// <summary>Item da listagem: sem o HTML, que só interessa no detalhe.</summary>
public sealed record TemplateListItemResponse(
    Guid Id,
    string Nome,
    string Categoria,
    IReadOnlyList<string> Tags,
    int TotalDeVersoes,
    int? VersaoAtual,
    IReadOnlyList<string> Variaveis,
    bool Arquivado,
    DateTimeOffset AtualizadoEm);

public sealed record TemplateDetailResponse(
    Guid Id,
    string Nome,
    string Categoria,
    IReadOnlyList<string> Tags,
    bool Arquivado,
    Guid CriadoPor,
    DateTimeOffset CriadoEm,
    DateTimeOffset AtualizadoEm,
    IReadOnlyList<TemplateVersionResponse> Versoes);

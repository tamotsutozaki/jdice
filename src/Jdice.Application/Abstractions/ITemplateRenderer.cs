namespace Jdice.Application.Abstractions;

/// <param name="Message">Mensagem já legível para quem escreveu o modelo.</param>
/// <param name="Line">Linha do erro, quando o motor informa.</param>
public sealed record TemplateError(string Message, int? Line);

/// <param name="Variables">Nomes declarados no conteúdo, na ordem em que aparecem.</param>
/// <param name="Errors">Vazio quando o conteúdo é válido.</param>
public sealed record TemplateAnalysis(
    IReadOnlyList<string> Variables,
    IReadOnlyList<TemplateError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed record TemplateRenderResult(
    string? Html,
    IReadOnlyList<TemplateError> Errors)
{
    public bool Succeeded => Errors.Count == 0 && Html is not null;
}

public interface ITemplateRenderer
{
    /// <summary>Descobre as variáveis declaradas e valida a sintaxe, sem renderizar.</summary>
    TemplateAnalysis Analyze(string html);

    /// <summary>Substitui as variáveis pelos valores informados.</summary>
    TemplateRenderResult Render(string html, IReadOnlyDictionary<string, string?> values);
}

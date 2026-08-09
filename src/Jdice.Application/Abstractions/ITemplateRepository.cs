using Jdice.Domain.Templates;

namespace Jdice.Application.Abstractions;

/// <param name="Busca">Filtro por nome, sem distinção de caixa. Vazio traz tudo.</param>
/// <param name="Categoria">Filtro exato por categoria. Vazio traz tudo.</param>
/// <param name="Tag">Filtro por tag. Vazio traz tudo.</param>
/// <param name="IncluirArquivados">Arquivados ficam fora por padrão.</param>
public sealed record TemplateFilter(
    string? Busca = null,
    string? Categoria = null,
    string? Tag = null,
    bool IncluirArquivados = false);

public interface ITemplateRepository
{
    Task<IReadOnlyList<Template>> ListAsync(
        TemplateFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>Traz o modelo com todas as versões carregadas.</summary>
    Task<Template?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(
        string name,
        Guid? exceptId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Categorias já usadas, para alimentar sugestões na interface.</summary>
    Task<IReadOnlyList<string>> ListCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Tags já usadas, para o filtro e as sugestões da interface.</summary>
    Task<IReadOnlyList<string>> ListTagsAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Template template, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

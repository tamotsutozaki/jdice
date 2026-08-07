using Jdice.Application.Abstractions;
using Jdice.Domain.Templates;

namespace Jdice.Application.Templates;

public sealed class TemplateService(
    ITemplateRepository templates,
    ITemplateRenderer renderer,
    TimeProvider clock)
{
    /// <summary>
    /// Quantas vezes tentar de novo quando duas versões disputam o mesmo
    /// número. Uma colisão é plausível; várias seguidas indicariam outra coisa.
    /// </summary>
    private const int TentativasDeVersao = 3;

    public Task<IReadOnlyList<Template>> ListAsync(
        TemplateFilter filter,
        CancellationToken cancellationToken = default) =>
        templates.ListAsync(filter, cancellationToken);

    public Task<IReadOnlyList<string>> ListCategoriesAsync(
        CancellationToken cancellationToken = default) =>
        templates.ListCategoriesAsync(cancellationToken);

    public async Task<Template> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await templates.FindByIdAsync(id, cancellationToken)
        ?? throw new TemplateNotFoundException(id);

    /// <summary>Cria o modelo já com a primeira versão do conteúdo.</summary>
    /// <exception cref="TemplateNameAlreadyInUseException">Nome já usado.</exception>
    /// <exception cref="InvalidTemplateContentException">Conteúdo com erro de sintaxe.</exception>
    public async Task<Template> CreateAsync(
        string name,
        string category,
        IEnumerable<string>? tags,
        string html,
        Guid createdBy,
        CancellationToken cancellationToken = default)
    {
        if (await templates.NameExistsAsync(name.Trim(), cancellationToken: cancellationToken))
        {
            throw new TemplateNameAlreadyInUseException(name.Trim());
        }

        var variaveis = AnalisarConteudo(html);
        var agora = clock.GetUtcNow();

        var template = Template.Create(name, category, tags, createdBy, agora);
        template.AddVersion(html, variaveis, createdBy, agora);

        await templates.AddAsync(template, cancellationToken);
        await templates.SaveChangesAsync(cancellationToken);

        return template;
    }

    /// <summary>
    /// Congela uma nova versão do conteúdo. É o único caminho para alterar o
    /// que o modelo envia — versões existentes nunca são modificadas.
    /// </summary>
    public async Task<TemplateVersion> AddVersionAsync(
        Guid templateId,
        string html,
        Guid createdBy,
        CancellationToken cancellationToken = default)
    {
        var variaveis = AnalisarConteudo(html);

        for (var tentativa = 1; ; tentativa++)
        {
            var template = await templates.FindByIdAsync(templateId, cancellationToken)
                ?? throw new TemplateNotFoundException(templateId);

            if (template.IsArchived)
            {
                throw new ArchivedTemplateException(templateId);
            }

            var version = template.AddVersion(html, variaveis, createdBy, clock.GetUtcNow());

            try
            {
                await templates.SaveChangesAsync(cancellationToken);
                return version;
            }
            catch (TemplateVersionConflictException) when (tentativa < TentativasDeVersao)
            {
                // Outra requisição criou uma versão entre a leitura e a
                // gravação. Recarrega e tenta com o número seguinte.
            }
        }
    }

    /// <summary>Altera apenas nome, categoria e tags. O conteúdo é intocado.</summary>
    public async Task<Template> UpdateDetailsAsync(
        Guid templateId,
        string name,
        string category,
        IEnumerable<string>? tags,
        CancellationToken cancellationToken = default)
    {
        var template = await templates.FindByIdAsync(templateId, cancellationToken)
            ?? throw new TemplateNotFoundException(templateId);

        if (await templates.NameExistsAsync(name.Trim(), templateId, cancellationToken))
        {
            throw new TemplateNameAlreadyInUseException(name.Trim());
        }

        template.UpdateDetails(name, category, tags, clock.GetUtcNow());

        await templates.SaveChangesAsync(cancellationToken);

        return template;
    }

    public async Task ArchiveAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var template = await templates.FindByIdAsync(templateId, cancellationToken)
            ?? throw new TemplateNotFoundException(templateId);

        template.Archive(clock.GetUtcNow());

        await templates.SaveChangesAsync(cancellationToken);
    }

    public async Task UnarchiveAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var template = await templates.FindByIdAsync(templateId, cancellationToken)
            ?? throw new TemplateNotFoundException(templateId);

        template.Unarchive(clock.GetUtcNow());

        await templates.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Renderiza um conteúdo com valores de exemplo, sem gravar nada.</summary>
    public TemplateRenderResult Preview(string html, IReadOnlyDictionary<string, string?> valores) =>
        renderer.Render(html, valores);

    public TemplateAnalysis Analyze(string html) => renderer.Analyze(html);

    private IReadOnlyList<string> AnalisarConteudo(string html)
    {
        var analise = renderer.Analyze(html);

        if (!analise.IsValid)
        {
            // Recusar na entrada evita guardar um modelo que nunca vai
            // renderizar — e a versão, uma vez criada, não pode ser corrigida.
            throw new InvalidTemplateContentException(
                [.. analise.Errors.Select(erro =>
                    erro.Line is null ? erro.Message : $"Linha {erro.Line}: {erro.Message}")]);
        }

        return analise.Variables;
    }
}

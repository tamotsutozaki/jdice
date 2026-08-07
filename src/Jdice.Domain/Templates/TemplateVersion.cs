namespace Jdice.Domain.Templates;

/// <summary>
/// Uma versão congelada do conteúdo de um modelo. Não existe — e não deve
/// passar a existir — nenhum método que altere o HTML depois de criado.
/// <para>
/// A imutabilidade é o motivo de a entidade existir: sem ela, a pergunta "o
/// que exatamente foi disparado em julho?" fica sem resposta. No projeto Java
/// original isso era apenas convenção, e o upload gravava por cima do arquivo
/// quando a versão informada era a mesma.
/// </para>
/// </summary>
public sealed class TemplateVersion
{
    // Exigido pelo EF Core para materializar a entidade.
    private TemplateVersion()
    {
        Html = null!;
        Variables = null!;
    }

    private TemplateVersion(
        Guid id,
        Guid templateId,
        int number,
        string html,
        IReadOnlyList<string> variables,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        Id = id;
        TemplateId = templateId;
        Number = number;
        Html = html;
        Variables = variables;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid TemplateId { get; private set; }

    /// <summary>Sequencial dentro do modelo, começando em 1.</summary>
    public int Number { get; private set; }

    public string Html { get; private set; }

    /// <summary>
    /// Nomes das variáveis declaradas no HTML, extraídos no momento da
    /// criação. Guardar é seguro justamente porque o HTML nunca muda — não há
    /// como a lista sair de sincronia com o conteúdo.
    /// </summary>
    public IReadOnlyList<string> Variables { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static TemplateVersion Create(
        Guid templateId,
        int number,
        string html,
        IReadOnlyList<string> variables,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new ArgumentException("O conteúdo do modelo é obrigatório.", nameof(html));
        }

        if (number < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(number), "A numeração começa em 1.");
        }

        return new TemplateVersion(
            Guid.CreateVersion7(createdAt),
            templateId,
            number,
            html,
            variables,
            createdBy,
            createdAt);
    }
}

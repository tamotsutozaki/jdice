namespace Jdice.Application.Templates;

public sealed class TemplateNotFoundException(Guid templateId)
    : Exception($"Não existe modelo com o identificador '{templateId}'.")
{
    public Guid TemplateId { get; } = templateId;
}

public sealed class TemplateNameAlreadyInUseException(string name)
    : Exception($"Já existe um modelo chamado '{name}'.")
{
    public string Name { get; } = name;
}

/// <summary>
/// Conteúdo recusado pelo motor de template — sintaxe inválida. Carrega as
/// mensagens do motor para que quem escreveu saiba o que corrigir.
/// </summary>
public sealed class InvalidTemplateContentException(IReadOnlyList<string> erros)
    : Exception("O conteúdo do modelo tem erros de sintaxe.")
{
    public IReadOnlyList<string> Erros { get; } = erros;
}

/// <summary>
/// Duas versões do mesmo modelo foram criadas ao mesmo tempo e disputaram o
/// mesmo número. Sinaliza para tentar de novo, não para desistir.
/// </summary>
public sealed class TemplateVersionConflictException()
    : Exception("Outra versão deste modelo foi criada ao mesmo tempo.");

public sealed class ArchivedTemplateException(Guid templateId)
    : Exception("Não é possível adicionar versão a um modelo arquivado.")
{
    public Guid TemplateId { get; } = templateId;
}

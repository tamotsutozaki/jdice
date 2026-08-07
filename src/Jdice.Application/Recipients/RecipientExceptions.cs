namespace Jdice.Application.Recipients;

public sealed class RecipientNotFoundException(Guid recipientId)
    : Exception($"Não existe destinatário com o identificador '{recipientId}'.")
{
    public Guid RecipientId { get; } = recipientId;
}

public sealed class RecipientListNotFoundException(Guid listId)
    : Exception($"Não existe lista com o identificador '{listId}'.")
{
    public Guid ListId { get; } = listId;
}

public sealed class RecipientEmailAlreadyInUseException(string email)
    : Exception($"Já existe um destinatário com o e-mail '{email}'.")
{
    public string Email { get; } = email;
}

public sealed class RecipientListNameAlreadyInUseException(string name)
    : Exception($"Já existe uma lista chamada '{name}'.")
{
    public string Name { get; } = name;
}

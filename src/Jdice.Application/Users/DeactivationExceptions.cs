namespace Jdice.Application.Users;

/// <summary>
/// Tentativa de desativar a própria conta. Quem fizesse isso se trancaria para
/// fora no meio da operação, e ainda por cima sem aviso.
/// </summary>
public sealed class CannotDeactivateSelfException()
    : Exception("Não é possível desativar a própria conta.");

/// <summary>
/// Tentativa de desativar o último administrador que ainda pode entrar. O
/// sistema ficaria sem ninguém capaz de criar contas ou administrar nada, e a
/// única saída seria alterar o banco na mão.
/// </summary>
public sealed class LastAdministratorException()
    : Exception("Não é possível desativar o último administrador ativo.");

/// <summary>Conta não encontrada para a operação pedida.</summary>
public sealed class UserNotFoundException(Guid userId)
    : Exception($"Não existe conta com o identificador '{userId}'.")
{
    public Guid UserId { get; } = userId;
}

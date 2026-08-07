namespace Jdice.Application.Users;

/// <summary>
/// Tentativa de criar conta com e-mail que já existe. É excepcional de verdade
/// — o front valida antes —, então vale como exceção e vira 409 na API.
/// </summary>
public sealed class EmailAlreadyInUseException(string email)
    : Exception($"Já existe uma conta com o e-mail '{email}'.")
{
    public string Email { get; } = email;
}

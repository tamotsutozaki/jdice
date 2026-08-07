namespace Jdice.Domain.Users;

/// <summary>
/// Fonte única da regra de senha. Antes o mesmo número aparecia no contrato da
/// API, no serviço de aplicação e no seed — três lugares para sair de sincronia
/// na primeira vez que alguém decidisse mudá-lo.
/// </summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 12;

    public const int MaximumLength = 128;

    public static bool IsValid(string? password) =>
        password is not null
        && password.Length >= MinimumLength
        && password.Length <= MaximumLength;

    /// <exception cref="ArgumentException">A senha não atende à política.</exception>
    public static void EnsureValid(string? password, string parameterName = "password")
    {
        if (!IsValid(password))
        {
            throw new ArgumentException(
                $"A senha precisa ter entre {MinimumLength} e {MaximumLength} caracteres.",
                parameterName);
        }
    }
}

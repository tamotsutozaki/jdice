namespace Jdice.Application.Users;

/// <summary>
/// A senha atual informada na troca de senha não confere. Trocar a senha exige
/// provar que se conhece a atual — senão uma sessão sequestrada trocaria a senha
/// e tomaria a conta de vez.
/// </summary>
public sealed class InvalidCurrentPasswordException()
    : Exception("A senha atual está incorreta.");

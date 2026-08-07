using Jdice.Application.Abstractions;
using Jdice.Domain.Users;

namespace Jdice.Application.Users;

public sealed class AuthenticationService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenService tokenService)
{
    /// <summary>
    /// Hash descartável de uma senha qualquer. Quando o e-mail não existe,
    /// verificamos a senha contra ele só para gastar o mesmo tempo de CPU do
    /// caminho feliz — sem isso, dá para descobrir quais e-mails têm conta
    /// medindo o tempo de resposta.
    /// </summary>
    private readonly string _dummyHash = passwordHasher.Hash("senha-que-nao-existe");

    /// <returns>O token, ou <c>null</c> se e-mail ou senha estiverem errados.</returns>
    public async Task<AccessToken?> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await users.FindByEmailAsync(User.NormalizeEmail(email), cancellationToken);

        if (user is null)
        {
            passwordHasher.Verify(password, _dummyHash);
            return null;
        }

        return passwordHasher.Verify(password, user.PasswordHash)
            ? tokenService.Issue(user)
            : null;
    }
}

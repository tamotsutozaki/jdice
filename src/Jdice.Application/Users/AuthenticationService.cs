using Jdice.Application.Abstractions;
using Jdice.Domain.Users;

namespace Jdice.Application.Users;

public sealed class AuthenticationService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenService tokenService)
{
    /// <returns>O token, ou <c>null</c> se e-mail ou senha estiverem errados.</returns>
    public async Task<AccessToken?> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await users.FindByEmailAsync(User.NormalizeEmail(email), cancellationToken);

        if (user is null)
        {
            // Verifica contra um hash de referência só para gastar o mesmo
            // tempo de CPU do caminho em que a conta existe. O hash já vem
            // pronto do hasher, que é singleton: gerá-lo aqui custaria um
            // BCrypt inteiro a mais em toda tentativa de login.
            passwordHasher.Verify(password, passwordHasher.ReferenceHash);
            return null;
        }

        return passwordHasher.Verify(password, user.PasswordHash)
            ? tokenService.Issue(user)
            : null;
    }
}

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

        if (!passwordHasher.Verify(password, user.PasswordHash))
        {
            return null;
        }

        // Conta desativada recebe a mesma resposta de senha errada. Dizer
        // "sua conta foi desativada" confirmaria a existência do e-mail para
        // quem só está tentando adivinhar.
        return user.IsActive ? tokenService.Issue(user) : null;
    }
}

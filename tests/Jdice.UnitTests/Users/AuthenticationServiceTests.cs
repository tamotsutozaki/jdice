using Jdice.Application.Users;
using Jdice.Domain.Users;
using Jdice.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Jdice.UnitTests.Users;

public class AuthenticationServiceTests
{
    private const string SenhaCorreta = "senha-bem-comprida-123";

    private static readonly DateTimeOffset Agora = new(2026, 8, 7, 15, 0, 0, TimeSpan.Zero);

    private readonly FakeUserRepository _users = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly AuthenticationService _authentication;

    public AuthenticationServiceTests()
    {
        var tokenService = new JwtTokenService(
            Options.Create(new JwtOptions
            {
                SigningKey = "chave-de-teste-com-mais-de-32-bytes-garantidos"
            }),
            new FakeTimeProvider(Agora));

        _authentication = new AuthenticationService(_users, _hasher, tokenService);
    }

    private User CadastrarUsuario(string email = "pedro@empresa.com", UserRole role = UserRole.User)
    {
        var user = User.Create(email, _hasher.Hash(SenhaCorreta), role, Agora);
        _users.Seed(user);
        return user;
    }

    [Fact]
    public async Task Login_com_credencial_correta_devolve_token()
    {
        CadastrarUsuario();

        var token = await _authentication.LoginAsync("pedro@empresa.com", SenhaCorreta);

        Assert.NotNull(token);
        Assert.NotEmpty(token.Value);
        Assert.Equal(Agora.AddHours(8), token.ExpiresAt);
    }

    [Fact]
    public async Task Login_aceita_email_com_caixa_e_espacos_diferentes()
    {
        CadastrarUsuario();

        var token = await _authentication.LoginAsync("  Pedro@Empresa.COM ", SenhaCorreta);

        Assert.NotNull(token);
    }

    [Fact]
    public async Task Login_com_senha_errada_e_recusado()
    {
        CadastrarUsuario();

        Assert.Null(await _authentication.LoginAsync("pedro@empresa.com", "senha-errada-123456"));
    }

    [Fact]
    public async Task Login_com_email_inexistente_e_recusado()
    {
        CadastrarUsuario();

        Assert.Null(await _authentication.LoginAsync("ninguem@empresa.com", SenhaCorreta));
    }
}

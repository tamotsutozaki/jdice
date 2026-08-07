using Jdice.Application.Users;
using Jdice.Domain.Users;
using Jdice.Infrastructure.Security;
using Microsoft.Extensions.Time.Testing;

namespace Jdice.UnitTests.Users;

public class DesativacaoDeContaTests
{
    private const string Senha = "senha-bem-comprida-123";

    private static readonly DateTimeOffset Agora = new(2026, 8, 7, 15, 0, 0, TimeSpan.Zero);

    private readonly FakeUserRepository _users = new();
    private readonly UserService _userService;

    public DesativacaoDeContaTests()
    {
        _userService = new UserService(_users, new BcryptPasswordHasher(), new FakeTimeProvider(Agora));
    }

    private User Cadastrar(string email, UserRole role, bool ativo = true)
    {
        var user = User.Create(email, "hash", role, Agora);

        if (!ativo)
        {
            user.Deactivate(Agora);
        }

        _users.Seed(user);
        return user;
    }

    [Fact]
    public async Task Admin_nao_pode_desativar_a_propria_conta()
    {
        var admin = Cadastrar("admin@empresa.com", UserRole.Admin);
        Cadastrar("outro@empresa.com", UserRole.Admin);

        // Quem se desativasse ficaria trancado para fora no meio da operação.
        await Assert.ThrowsAsync<CannotDeactivateSelfException>(
            () => _userService.DeactivateAsync(admin.Id, requestedBy: admin.Id));

        Assert.True(admin.IsActive);
    }

    [Fact]
    public async Task Ultimo_administrador_ativo_nao_pode_ser_desativado()
    {
        var unico = Cadastrar("admin@empresa.com", UserRole.Admin);
        var quemPede = Cadastrar("outro-admin@empresa.com", UserRole.Admin, ativo: false);

        // Sobrando zero administrador, ninguém mais criaria contas nem
        // administraria nada — a única saída seria mexer no banco na mão.
        await Assert.ThrowsAsync<LastAdministratorException>(
            () => _userService.DeactivateAsync(unico.Id, requestedBy: quemPede.Id));

        Assert.True(unico.IsActive);
    }

    [Fact]
    public async Task Administrador_pode_ser_desativado_quando_ha_outro_ativo()
    {
        var alvo = Cadastrar("admin1@empresa.com", UserRole.Admin);
        var quemPede = Cadastrar("admin2@empresa.com", UserRole.Admin);

        await _userService.DeactivateAsync(alvo.Id, requestedBy: quemPede.Id);

        Assert.False(alvo.IsActive);
        Assert.Equal(Agora, alvo.DeactivatedAt);
    }

    [Fact]
    public async Task Usuario_comum_pode_ser_desativado_mesmo_com_um_unico_admin()
    {
        var comum = Cadastrar("comum@empresa.com", UserRole.User);
        var admin = Cadastrar("admin@empresa.com", UserRole.Admin);

        // A proteção é sobre administradores; desativar um usuário comum não
        // deixa o sistema sem quem o administre.
        await _userService.DeactivateAsync(comum.Id, requestedBy: admin.Id);

        Assert.False(comum.IsActive);
    }

    [Fact]
    public async Task Desativar_conta_inexistente_e_recusado()
    {
        var admin = Cadastrar("admin@empresa.com", UserRole.Admin);

        await Assert.ThrowsAsync<UserNotFoundException>(
            () => _userService.DeactivateAsync(Guid.CreateVersion7(), requestedBy: admin.Id));
    }

    [Fact]
    public async Task Desativar_duas_vezes_nao_e_erro_e_preserva_a_data_original()
    {
        var comum = Cadastrar("comum@empresa.com", UserRole.User);
        var admin = Cadastrar("admin@empresa.com", UserRole.Admin);

        await _userService.DeactivateAsync(comum.Id, requestedBy: admin.Id);
        var primeiraData = comum.DeactivatedAt;

        // Clicar duas vezes não pode virar erro nem reescrever quando aconteceu.
        await _userService.DeactivateAsync(comum.Id, requestedBy: admin.Id);

        Assert.Equal(primeiraData, comum.DeactivatedAt);
    }

    [Fact]
    public async Task Conta_desativada_nao_consegue_entrar()
    {
        var hasher = new BcryptPasswordHasher();
        var users = new FakeUserRepository();
        var user = User.Create("comum@empresa.com", hasher.Hash(Senha), UserRole.User, Agora);
        user.Deactivate(Agora);
        users.Seed(user);

        var tokenService = new JwtTokenService(
            Microsoft.Extensions.Options.Options.Create(new JwtOptions
            {
                SigningKey = "chave-de-teste-com-mais-de-32-bytes-garantidos"
            }),
            new FakeTimeProvider(Agora));

        var authentication = new AuthenticationService(users, hasher, tokenService);

        // Mesma resposta de senha errada: dizer "conta desativada" confirmaria
        // a existência do e-mail para quem só está tentando adivinhar.
        Assert.Null(await authentication.LoginAsync("comum@empresa.com", Senha));
    }

    [Fact]
    public void Reativar_devolve_o_acesso()
    {
        var user = User.Create("comum@empresa.com", "hash", UserRole.User, Agora);

        user.Deactivate(Agora);
        Assert.False(user.IsActive);

        user.Reactivate();
        Assert.True(user.IsActive);
        Assert.Null(user.DeactivatedAt);
    }
}

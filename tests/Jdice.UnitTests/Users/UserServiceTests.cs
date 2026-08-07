using Jdice.Application.Users;
using Jdice.Domain.Users;
using Jdice.Infrastructure.Security;
using Microsoft.Extensions.Time.Testing;

namespace Jdice.UnitTests.Users;

public class UserServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 7, 15, 0, 0, TimeSpan.Zero);

    private readonly FakeUserRepository _users = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly UserService _userService;

    public UserServiceTests()
    {
        _userService = new UserService(_users, _hasher, new FakeTimeProvider(Agora));
    }

    [Fact]
    public async Task Criar_usuario_guarda_hash_e_nao_a_senha()
    {
        const string senha = "senha-bem-comprida-123";

        var user = await _userService.CreateAsync("pedro@empresa.com", senha, UserRole.User);

        Assert.NotEqual(senha, user.PasswordHash);
        Assert.True(_hasher.Verify(senha, user.PasswordHash));
        Assert.Equal(1, _users.SaveChangesCount);
    }

    [Fact]
    public async Task Criar_usuario_normaliza_o_email()
    {
        var user = await _userService.CreateAsync("  Pedro@Empresa.COM ", "senha-bem-comprida-123", UserRole.User);

        Assert.Equal("pedro@empresa.com", user.Email);
    }

    [Fact]
    public async Task Criar_usuario_com_email_repetido_e_recusado()
    {
        await _userService.CreateAsync("pedro@empresa.com", "senha-bem-comprida-123", UserRole.User);

        await Assert.ThrowsAsync<EmailAlreadyInUseException>(
            () => _userService.CreateAsync("pedro@empresa.com", "outra-senha-comprida", UserRole.Admin));
    }

    [Fact]
    public async Task Duplicidade_de_email_ignora_diferenca_de_caixa()
    {
        await _userService.CreateAsync("pedro@empresa.com", "senha-bem-comprida-123", UserRole.User);

        await Assert.ThrowsAsync<EmailAlreadyInUseException>(
            () => _userService.CreateAsync("PEDRO@EMPRESA.COM", "outra-senha-comprida", UserRole.User));
    }

    [Fact]
    public async Task Role_pedida_e_a_role_gravada()
    {
        var admin = await _userService.CreateAsync("admin@empresa.com", "senha-bem-comprida-123", UserRole.Admin);

        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.Equal(Agora, admin.CreatedAt);
    }
}

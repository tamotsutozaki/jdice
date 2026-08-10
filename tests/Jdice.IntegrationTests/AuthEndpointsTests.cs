using System.Net;
using System.Net.Http.Json;
using Jdice.Domain.Users;

namespace Jdice.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class AuthEndpointsTests(JdiceApiFactory factory) : IAsyncLifetime
{
    private const string SenhaValida = "senha-bem-comprida-123";

    public Task InitializeAsync() => factory.ResetUsersAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Cliente que guarda cookies, para que o fluxo real seja exercitado:
    /// o login grava o cookie de sessão e a próxima chamada o reenvia.
    /// </summary>
    private HttpClient CriarCliente() => factory.CreateClient(new()
    {
        HandleCookies = true
    });

    private async Task<HttpClient> LogarComoAsync(string email, UserRole role)
    {
        await factory.CriarUsuarioAsync(email, SenhaValida, role);

        var client = CriarCliente();
        var resposta = await client.PostAsJsonAsync("/api/auth/login", new { email, senha = SenhaValida });

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);

        return client;
    }

    [Fact]
    public async Task Ready_confirma_que_a_api_alcanca_o_banco()
    {
        var resposta = await CriarCliente().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("Healthy", await resposta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_com_credencial_correta_devolve_cookie_httpOnly()
    {
        await factory.CriarUsuarioAsync("pedro@empresa.com", SenhaValida, UserRole.User);

        var resposta = await CriarCliente()
            .PostAsJsonAsync("/api/auth/login", new { email = "pedro@empresa.com", senha = SenhaValida });

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);

        var setCookie = Assert.Single(resposta.Headers.GetValues("Set-Cookie"));

        Assert.Contains("jdice_auth=", setCookie, StringComparison.Ordinal);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);

        // O corpo não pode conter o token: se ele chegasse ao JavaScript, o
        // cookie httpOnly não teria servido para nada.
        Assert.Empty(await resposta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_com_senha_errada_devolve_401_sem_cookie()
    {
        await factory.CriarUsuarioAsync("pedro@empresa.com", SenhaValida, UserRole.User);

        var resposta = await CriarCliente()
            .PostAsJsonAsync("/api/auth/login", new { email = "pedro@empresa.com", senha = "senha-errada-123456" });

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.False(resposta.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Login_com_email_inexistente_devolve_401()
    {
        var resposta = await CriarCliente()
            .PostAsJsonAsync("/api/auth/login", new { email = "ninguem@empresa.com", senha = SenhaValida });

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Me_sem_cookie_devolve_401()
    {
        var resposta = await CriarCliente().GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Me_com_sessao_devolve_o_usuario_logado()
    {
        var client = await LogarComoAsync("pedro@empresa.com", UserRole.User);

        var usuario = await client.GetFromJsonAsync<CurrentUser>("/api/auth/me");

        Assert.NotNull(usuario);
        Assert.Equal("pedro@empresa.com", usuario.Email);
        Assert.Equal("User", usuario.Role);
    }

    [Fact]
    public async Task Trocar_senha_com_a_atual_correta_passa_a_valer_no_login()
    {
        var client = await LogarComoAsync("pedro@empresa.com", UserRole.User);

        var troca = await client.PostAsJsonAsync(
            "/api/auth/me/password",
            new { senhaAtual = SenhaValida, novaSenha = "outra-senha-bem-longa-456" });

        Assert.Equal(HttpStatusCode.NoContent, troca.StatusCode);

        // A senha antiga deixa de funcionar e a nova passa a valer.
        var comAntiga = await CriarCliente()
            .PostAsJsonAsync("/api/auth/login", new { email = "pedro@empresa.com", senha = SenhaValida });
        Assert.Equal(HttpStatusCode.Unauthorized, comAntiga.StatusCode);

        var comNova = await CriarCliente().PostAsJsonAsync(
            "/api/auth/login",
            new { email = "pedro@empresa.com", senha = "outra-senha-bem-longa-456" });
        Assert.Equal(HttpStatusCode.NoContent, comNova.StatusCode);
    }

    [Fact]
    public async Task Trocar_senha_com_a_atual_errada_devolve_400()
    {
        var client = await LogarComoAsync("pedro@empresa.com", UserRole.User);

        var troca = await client.PostAsJsonAsync(
            "/api/auth/me/password",
            new { senhaAtual = "chute-errado-comprido", novaSenha = "outra-senha-bem-longa-456" });

        // Sem provar a senha atual, uma sessão sequestrada tomaria a conta.
        Assert.Equal(HttpStatusCode.BadRequest, troca.StatusCode);
    }

    [Fact]
    public async Task Trocar_senha_sem_estar_logado_devolve_401()
    {
        var troca = await CriarCliente().PostAsJsonAsync(
            "/api/auth/me/password",
            new { senhaAtual = SenhaValida, novaSenha = "outra-senha-bem-longa-456" });

        Assert.Equal(HttpStatusCode.Unauthorized, troca.StatusCode);
    }

    [Fact]
    public async Task Logout_encerra_a_sessao()
    {
        var client = await LogarComoAsync("pedro@empresa.com", UserRole.User);

        var logout = await client.PostAsync("/api/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var depois = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, depois.StatusCode);
    }

    [Fact]
    public async Task Criar_usuario_sem_estar_logado_devolve_401()
    {
        // No projeto original este endpoint era público e ainda aceitava a role
        // no corpo: qualquer pessoa da internet criava um administrador.
        var resposta = await CriarCliente().PostAsJsonAsync(
            "/api/auth/users",
            new { email = "invasor@empresa.com", senha = SenhaValida, role = "Admin" });

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Criar_usuario_logado_como_User_comum_devolve_403()
    {
        var client = await LogarComoAsync("comum@empresa.com", UserRole.User);

        var resposta = await client.PostAsJsonAsync(
            "/api/auth/users",
            new { email = "novo@empresa.com", senha = SenhaValida, role = "User" });

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Criar_usuario_como_Admin_devolve_201()
    {
        var client = await LogarComoAsync("admin@empresa.com", UserRole.Admin);

        var resposta = await client.PostAsJsonAsync(
            "/api/auth/users",
            new { email = "novo@empresa.com", senha = SenhaValida, role = "User" });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        var criado = await resposta.Content.ReadFromJsonAsync<CurrentUser>();
        Assert.NotNull(criado);
        Assert.Equal("novo@empresa.com", criado.Email);
        Assert.Equal("User", criado.Role);
    }

    [Fact]
    public async Task Criar_usuario_com_email_repetido_devolve_409()
    {
        var client = await LogarComoAsync("admin@empresa.com", UserRole.Admin);

        var payload = new { email = "repetido@empresa.com", senha = SenhaValida, role = "User" };

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/users", payload)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/auth/users", payload)).StatusCode);
    }

    [Fact]
    public async Task Criar_usuario_com_senha_curta_devolve_400()
    {
        var client = await LogarComoAsync("admin@empresa.com", UserRole.Admin);

        var resposta = await client.PostAsJsonAsync(
            "/api/auth/users",
            new { email = "novo@empresa.com", senha = "curta", role = "User" });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    private sealed record CurrentUser(Guid Id, string Email, string Role);
}

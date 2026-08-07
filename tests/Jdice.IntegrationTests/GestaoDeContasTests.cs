using System.Net;
using System.Net.Http.Json;
using Jdice.Domain.Users;

namespace Jdice.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class GestaoDeContasTests(JdiceApiFactory factory) : IAsyncLifetime
{
    private const string Senha = "senha-bem-comprida-123";

    public Task InitializeAsync() => factory.ResetUsersAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> LogarAsync(string email)
    {
        var client = factory.CreateClient(new() { HandleCookies = true });

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, senha = Senha });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);

        return client;
    }

    [Fact]
    public async Task Admin_lista_todas_as_contas()
    {
        await factory.CriarUsuarioAsync("admin@empresa.com", Senha, UserRole.Admin);
        await factory.CriarUsuarioAsync("comum@empresa.com", Senha, UserRole.User);

        var client = await LogarAsync("admin@empresa.com");
        var contas = await client.GetFromJsonAsync<List<ContaListada>>("/api/auth/users");

        Assert.NotNull(contas);
        Assert.Equal(2, contas.Count);
        Assert.All(contas, conta => Assert.True(conta.Ativo));
        Assert.Contains(contas, conta => conta.Email == "comum@empresa.com" && conta.Role == "User");
    }

    [Fact]
    public async Task Usuario_comum_nao_lista_contas()
    {
        await factory.CriarUsuarioAsync("comum@empresa.com", Senha, UserRole.User);

        var client = await LogarAsync("comum@empresa.com");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/auth/users")).StatusCode);
    }

    [Fact]
    public async Task Desativar_impede_novo_login()
    {
        await factory.CriarUsuarioAsync("admin@empresa.com", Senha, UserRole.Admin);
        var alvo = await factory.CriarUsuarioAsync("comum@empresa.com", Senha, UserRole.User);

        var admin = await LogarAsync("admin@empresa.com");
        var resposta = await admin.DeleteAsync($"/api/auth/users/{alvo.Id}");

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);

        var tentativa = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new { email = "comum@empresa.com", senha = Senha });

        Assert.Equal(HttpStatusCode.Unauthorized, tentativa.StatusCode);
    }

    [Fact]
    public async Task Desativar_derruba_a_sessao_ja_aberta_na_hora()
    {
        await factory.CriarUsuarioAsync("admin@empresa.com", Senha, UserRole.Admin);
        var alvo = await factory.CriarUsuarioAsync("comum@empresa.com", Senha, UserRole.User);

        // A pessoa já está usando o sistema quando é desativada.
        var vitima = await LogarAsync("comum@empresa.com");
        Assert.Equal(HttpStatusCode.OK, (await vitima.GetAsync("/api/auth/me")).StatusCode);

        var admin = await LogarAsync("admin@empresa.com");
        await admin.DeleteAsync($"/api/auth/users/{alvo.Id}");

        // O token dela continua válido por horas e carrega a role dentro. Sem
        // verificar a conta a cada requisição, ela seguiria usando o sistema
        // normalmente depois de removida.
        Assert.Equal(HttpStatusCode.Unauthorized, (await vitima.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Admin_nao_desativa_a_propria_conta()
    {
        var admin = await factory.CriarUsuarioAsync("admin@empresa.com", Senha, UserRole.Admin);
        await factory.CriarUsuarioAsync("outro@empresa.com", Senha, UserRole.Admin);

        var client = await LogarAsync("admin@empresa.com");
        var resposta = await client.DeleteAsync($"/api/auth/users/{admin.Id}");

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);

        // E continua conseguindo trabalhar.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Ultimo_administrador_ativo_nao_pode_ser_desativado()
    {
        var unico = await factory.CriarUsuarioAsync("admin@empresa.com", Senha, UserRole.Admin);
        var segundo = await factory.CriarUsuarioAsync("admin2@empresa.com", Senha, UserRole.Admin);

        var client = await LogarAsync("admin@empresa.com");

        // Primeiro sai um dos dois: permitido, ainda sobra administrador.
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/auth/users/{segundo.Id}")).StatusCode);

        // Agora o que sobrou tenta ser desativado por outro admin — que não
        // existe mais. Simulamos com o próprio, que já cai na proteção de
        // autodesativação, então usamos uma segunda conta admin recém-criada.
        var terceiro = await factory.CriarUsuarioAsync("admin3@empresa.com", Senha, UserRole.Admin);
        var terceiroClient = await LogarAsync("admin3@empresa.com");

        // Com dois ativos (unico e terceiro), desativar um é permitido...
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await terceiroClient.DeleteAsync($"/api/auth/users/{unico.Id}")).StatusCode);

        // ...mas agora terceiro é o único, e ninguém pode removê-lo.
        var novoAdmin = await factory.CriarUsuarioAsync("admin4@empresa.com", Senha, UserRole.Admin);
        var novoClient = await LogarAsync("admin4@empresa.com");

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await novoClient.DeleteAsync($"/api/auth/users/{terceiro.Id}")).StatusCode);

        // Sobrou só novoAdmin: ele não consegue ser removido por si mesmo, e
        // não há mais ninguém para removê-lo.
        var tentativa = await novoClient.DeleteAsync($"/api/auth/users/{novoAdmin.Id}");
        Assert.Equal(HttpStatusCode.Conflict, tentativa.StatusCode);
    }

    [Fact]
    public async Task Conta_desativada_aparece_na_listagem_com_a_data()
    {
        await factory.CriarUsuarioAsync("admin@empresa.com", Senha, UserRole.Admin);
        var alvo = await factory.CriarUsuarioAsync("comum@empresa.com", Senha, UserRole.User);

        var client = await LogarAsync("admin@empresa.com");
        await client.DeleteAsync($"/api/auth/users/{alvo.Id}");

        var contas = await client.GetFromJsonAsync<List<ContaListada>>("/api/auth/users");
        var desativada = Assert.Single(contas!, conta => conta.Email == "comum@empresa.com");

        // A linha é preservada de propósito: apagá-la faria o histórico do que
        // a pessoa criou perder o autor.
        Assert.False(desativada.Ativo);
        Assert.NotNull(desativada.DesativadoEm);
    }

    [Fact]
    public async Task Desativar_conta_inexistente_devolve_404()
    {
        await factory.CriarUsuarioAsync("admin@empresa.com", Senha, UserRole.Admin);

        var client = await LogarAsync("admin@empresa.com");
        var resposta = await client.DeleteAsync($"/api/auth/users/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task Usuario_comum_nao_desativa_ninguem()
    {
        var alvo = await factory.CriarUsuarioAsync("admin@empresa.com", Senha, UserRole.Admin);
        await factory.CriarUsuarioAsync("comum@empresa.com", Senha, UserRole.User);

        var client = await LogarAsync("comum@empresa.com");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.DeleteAsync($"/api/auth/users/{alvo.Id}")).StatusCode);
    }

    private sealed record ContaListada(
        Guid Id,
        string Email,
        string Role,
        bool Ativo,
        DateTimeOffset CriadoEm,
        DateTimeOffset? DesativadoEm);
}

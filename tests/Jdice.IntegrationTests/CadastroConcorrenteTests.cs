using System.Net;
using System.Net.Http.Json;
using Jdice.Domain.Users;

namespace Jdice.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class CadastroConcorrenteTests(JdiceApiFactory factory) : IAsyncLifetime
{
    private const string SenhaValida = "senha-bem-comprida-123";

    public Task InitializeAsync() => factory.ResetUsersAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> ClienteAdminAsync()
    {
        await factory.CriarUsuarioAsync("admin@empresa.com", SenhaValida, UserRole.Admin);

        var client = factory.CreateClient(new() { HandleCookies = true });

        await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "admin@empresa.com", senha = SenhaValida });

        return client;
    }

    [Fact]
    public async Task Cadastros_simultaneos_do_mesmo_email_criam_uma_conta_so()
    {
        var client = await ClienteAdminAsync();

        var payload = new { email = "disputado@empresa.com", senha = SenhaValida, role = "User" };

        // A checagem prévia em memória não protege daqui: as duas requisições
        // podem passar por ela antes de qualquer INSERT. Quem impede a
        // duplicata é o índice único do banco — e a violação precisa virar 409,
        // não 500.
        var respostas = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync("/api/auth/users", payload)));

        var criadas = respostas.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflitos = respostas.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, criadas);
        Assert.Equal(respostas.Length - 1, conflitos);

        // Nenhuma pode ter virado erro do servidor: conflito tem resposta própria.
        Assert.DoesNotContain(respostas, r => r.StatusCode == HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Login_funciona_apos_a_disputa_de_cadastro()
    {
        var client = await ClienteAdminAsync();

        var payload = new { email = "disputado@empresa.com", senha = SenhaValida, role = "User" };

        await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync("/api/auth/users", payload)));

        // A conta que venceu a corrida precisa estar íntegra — se o
        // ChangeTracker tivesse ficado sujo depois da violação, o registro
        // poderia não ter sido gravado corretamente.
        var login = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new { email = "disputado@empresa.com", senha = SenhaValida });

        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
    }
}

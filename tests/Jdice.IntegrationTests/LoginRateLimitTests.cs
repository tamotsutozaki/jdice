using System.Net;
using System.Net.Http.Json;
using Jdice.Domain.Users;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Jdice.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class LoginRateLimitTests(JdiceApiFactory factory) : IAsyncLifetime
{
    private const int Limite = 3;
    private const string SenhaValida = "senha-bem-comprida-123";

    private ApiComLimite? _comLimite;

    public async Task InitializeAsync()
    {
        await factory.ResetUsersAsync();
        await factory.CriarUsuarioAsync("pedro@empresa.com", SenhaValida, UserRole.User);

        _comLimite = new ApiComLimite(factory.ConnectionString, Limite);
    }

    public Task DisposeAsync()
    {
        _comLimite?.Dispose();
        return Task.CompletedTask;
    }

    private static Task<HttpResponseMessage> TentarLogin(HttpClient client, string senha) =>
        client.PostAsJsonAsync("/api/auth/login", new { email = "pedro@empresa.com", senha });

    [Fact]
    public async Task Tentativas_acima_do_limite_recebem_429()
    {
        var client = _comLimite!.CreateClient();

        // Sem limite, este endpoint aceitaria força bruta à vontade — e como o
        // BCrypt é lento de propósito, cada tentativa custa CPU do servidor.
        for (var tentativa = 1; tentativa <= Limite; tentativa++)
        {
            var resposta = await TentarLogin(client, "senha-errada-123456");
            Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        }

        var excedente = await TentarLogin(client, "senha-errada-123456");

        Assert.Equal(HttpStatusCode.TooManyRequests, excedente.StatusCode);
    }

    [Fact]
    public async Task Limite_vale_mesmo_para_a_senha_correta()
    {
        var client = _comLimite!.CreateClient();

        for (var tentativa = 1; tentativa <= Limite; tentativa++)
        {
            await TentarLogin(client, "senha-errada-123456");
        }

        // Se acertar a senha contornasse o limite, bastaria ao atacante seguir
        // tentando: o bloqueio vale para a requisição, não para o resultado.
        var comSenhaCerta = await TentarLogin(client, SenhaValida);

        Assert.Equal(HttpStatusCode.TooManyRequests, comSenhaCerta.StatusCode);
    }

    [Fact]
    public async Task Sem_o_limitador_os_logins_seguem_livres()
    {
        // A fábrica compartilhada mantém o limitador desligado, senão as demais
        // classes de teste — que fazem vários logins seguidos — esbarrariam nele.
        var client = factory.CreateClient();

        for (var tentativa = 1; tentativa <= Limite + 5; tentativa++)
        {
            var resposta = await TentarLogin(client, SenhaValida);
            Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        }
    }

    /// <summary>
    /// Host próprio, não derivado por WithWebHostBuilder: uma fábrica derivada
    /// acabava afetando as requisições feitas pela fábrica compartilhada, e os
    /// testes das outras classes passavam a receber 429.
    /// </summary>
    private sealed class ApiComLimite(string connectionString, int limite) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Development);

            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Postgres"] = connectionString,
                    ["Jwt:SigningKey"] = SigningKey,
                    ["Database:AutoMigrate"] = "false",
                    ["Seed:AdminEmail"] = "",
                    ["Seed:AdminPassword"] = "",
                    ["RateLimiting:Login:Enabled"] = "true",
                    ["RateLimiting:Login:PermitLimit"] = limite.ToString(),
                    ["RateLimiting:Login:Window"] = "00:05:00"
                }));
        }

        private const string SigningKey = JdiceApiFactory.SigningKey;
    }
}

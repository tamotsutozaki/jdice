using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Jdice.IntegrationTests;

/// <summary>
/// Sobe a API sem banco nenhum, de propósito: /health/live existe justamente
/// para responder sem depender de infraestrutura.
/// </summary>
public class HealthEndpointsTests : IClassFixture<HealthEndpointsTests.SemBancoFactory>
{
    private readonly SemBancoFactory _factory;

    public HealthEndpointsTests(SemBancoFactory factory) => _factory = factory;

    [Fact]
    public async Task Live_responde_healthy_sem_depender_de_infraestrutura()
    {
        var response = await _factory.CreateClient().GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    public sealed class SemBancoFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Postgres"] = "",
                    ["Database:AutoMigrate"] = "false",
                    ["Jwt:SigningKey"] = JdiceApiFactory.SigningKey
                });
            });
        }
    }
}

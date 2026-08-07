using Jdice.Application.Users;
using Jdice.Domain.Users;
using Jdice.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Jdice.IntegrationTests;

/// <summary>
/// Sobe a API contra um Postgres real em container. Banco de verdade em vez de
/// in-memory porque o que precisa ser testado aqui — índice único de e-mail,
/// conversão de enum para texto, timestamptz — simplesmente não existe no
/// provider in-memory.
/// </summary>
public sealed class JdiceApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SigningKey = "chave-de-teste-de-integracao-com-mais-de-32-bytes";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("jdice_tests")
        .WithUsername("jdice")
        .WithPassword("jdice")
        .Build();

    /// <summary>
    /// Exposta para testes que precisam de um host próprio — como o do
    /// limitador de tentativas, que liga o que todos os outros precisam
    /// desligado — reaproveitando o mesmo container em vez de subir outro.
    /// </summary>
    public string ConnectionString => _postgres.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
                ["Jwt:SigningKey"] = SigningKey,
                ["Jwt:Lifetime"] = "08:00:00",

                // Cada teste prepara os dados de que precisa; um seed automático
                // deixaria uma conta admin surpresa em todos eles.
                ["Database:AutoMigrate"] = "false",
                ["Seed:AdminEmail"] = "",
                ["Seed:AdminPassword"] = "",

                // Os testes fazem muitos logins seguidos da mesma origem, o que
                // estouraria o limite. O limitador tem teste próprio, com a
                // fábrica configurada para exercitá-lo.
                ["RateLimiting:Login:Enabled"] = "false"
            });
        });
    }

    // Implementação explícita: o WebApplicationFactory já expõe um DisposeAsync
    // que devolve ValueTask, e o IAsyncLifetime do xUnit v2 pede Task. Sem
    // separar os dois, as assinaturas colidem.
    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JdiceDbContext>();
        await context.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>Apaga as contas para que um teste não enxergue o que o outro criou.</summary>
    public async Task ResetUsersAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JdiceDbContext>();
        await context.Users.ExecuteDeleteAsync();
    }

    public async Task ResetTemplatesAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JdiceDbContext>();

        // As versões saem em cascata pela chave estrangeira.
        await context.Templates.ExecuteDeleteAsync();
    }

    public async Task<User> CriarUsuarioAsync(string email, string senha, UserRole role)
    {
        using var scope = Services.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<UserService>();
        return await userService.CreateAsync(email, senha, role);
    }
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<JdiceApiFactory>;

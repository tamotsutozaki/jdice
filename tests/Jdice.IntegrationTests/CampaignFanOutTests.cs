using System.Net.Http.Json;
using System.Text;
using Jdice.Application.Abstractions;
using Jdice.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Jdice.IntegrationTests;

/// <summary>
/// Repartição do lote na fila. O caso que importa é o lote maior que o tamanho
/// da página: com poucos destinatários o defeito não aparece, e foi
/// exatamente assim que ele passou despercebido até um teste com 120.
/// </summary>
[Collection(nameof(CampanhaCompletaCollection))]
public class CampaignFanOutTests(CampanhaCompletaFixture factory) : IAsyncLifetime
{
    private const string Senha = "senha-bem-comprida-123";

    public async Task InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<Infrastructure.Persistence.JdiceDbContext>();

        await context.Campaigns.ExecuteDeleteAsync();
        await context.RecipientLists.ExecuteDeleteAsync();
        await context.Recipients.ExecuteDeleteAsync();
        await context.Templates.ExecuteDeleteAsync();
        await context.Users.ExecuteDeleteAsync();

        await factory.LimparCaixaAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Lote_maior_que_a_pagina_e_repartido_por_inteiro()
    {
        const int quantidade = 250;

        var client = await LogarAsync();
        var campanhaId = await MontarDisparoAsync(client, quantidade);

        var fila = new FilaQueRegistra();

        // Substitui a fila para observar o que seria publicado, sem precisar de
        // um broker de verdade só para verificar a repartição.
        using var scope = factory.Services.CreateScope();

        var processor = new Application.Campaigns.CampaignProcessor(
            scope.ServiceProvider.GetRequiredService<ICampaignRepository>(),
            scope.ServiceProvider.GetRequiredService<IRecipientRepository>(),
            scope.ServiceProvider.GetRequiredService<ITemplateRepository>(),
            scope.ServiceProvider.GetRequiredService<ITemplateRenderer>(),
            scope.ServiceProvider.GetRequiredService<IEmailSender>(),
            fila,
            TimeProvider.System,
            scope.ServiceProvider
                .GetRequiredService<Microsoft.Extensions.Logging.ILogger<
                    Application.Campaigns.CampaignProcessor>>());

        await processor.ProcessAsync(campanhaId);

        // Publicar não muda a situação da entrega. Sem percorrer por cursor, a
        // consulta traria sempre as primeiras e o resto nunca sairia.
        Assert.Equal(quantidade, fila.Publicadas.Count);
        Assert.Equal(quantidade, fila.Publicadas.Select(t => t.DeliveryId).Distinct().Count());
    }

    [Fact]
    public async Task Com_a_fila_ligada_o_job_nao_envia_direto()
    {
        var client = await LogarAsync();
        var campanhaId = await MontarDisparoAsync(client, 5);

        var fila = new FilaQueRegistra();
        using var scope = factory.Services.CreateScope();

        var processor = new Application.Campaigns.CampaignProcessor(
            scope.ServiceProvider.GetRequiredService<ICampaignRepository>(),
            scope.ServiceProvider.GetRequiredService<IRecipientRepository>(),
            scope.ServiceProvider.GetRequiredService<ITemplateRepository>(),
            scope.ServiceProvider.GetRequiredService<ITemplateRenderer>(),
            scope.ServiceProvider.GetRequiredService<IEmailSender>(),
            fila,
            TimeProvider.System,
            scope.ServiceProvider
                .GetRequiredService<Microsoft.Extensions.Logging.ILogger<
                    Application.Campaigns.CampaignProcessor>>());

        await processor.ProcessAsync(campanhaId);

        // Quem envia são os consumidores; o job apenas reparte.
        Assert.Empty(await factory.MensagensAsync());
        Assert.Equal(5, fila.Publicadas.Count);
    }

    private async Task<HttpClient> LogarAsync()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<Application.Users.UserService>();
            await users.CreateAsync("operador@empresa.com", Senha, UserRole.Admin);
        }

        var client = factory.CreateClient(new() { HandleCookies = true });
        await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "operador@empresa.com", senha = Senha });

        return client;
    }

    private static async Task<Guid> MontarDisparoAsync(HttpClient client, int quantidade)
    {
        var modelo = await client.PostAsJsonAsync("/api/templates", new
        {
            nome = $"Modelo {Guid.CreateVersion7()}",
            categoria = "Teste",
            tags = Array.Empty<string>(),
            html = "<p>Olá {{ nome }}.</p>"
        });
        var templateId = (await modelo.Content.ReadFromJsonAsync<IdResponse>())!.Id;

        var lista = await client.PostAsJsonAsync(
            "/api/recipient-lists",
            new { nome = $"Lista {Guid.CreateVersion7()}" });
        var listaId = (await lista.Content.ReadFromJsonAsync<IdResponse>())!.Id;

        var linhas = new StringBuilder("email;nome\n");

        for (var i = 1; i <= quantidade; i++)
        {
            linhas.Append($"pessoa{i:D4}@empresa.com;Pessoa {i}\n");
        }

        var arquivo = new ByteArrayContent(new UTF8Encoding(false).GetBytes(linhas.ToString()));
        arquivo.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");

        await client.PostAsync(
            $"/api/recipients/import?listaId={listaId}",
            new MultipartFormDataContent { { arquivo, "arquivo", "lista.csv" } });

        var criado = await client.PostAsJsonAsync("/api/campaigns", new
        {
            templateId,
            assunto = "Lote",
            listaIds = new[] { listaId }
        });

        return (await criado.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private sealed record IdResponse(Guid Id);

    private sealed class FilaQueRegistra : IDeliveryQueue
    {
        public List<DeliveryWork> Publicadas { get; } = [];

        public bool IsEnabled => true;

        public Task PublishAsync(
            IReadOnlyCollection<DeliveryWork> trabalhos,
            CancellationToken cancellationToken = default)
        {
            Publicadas.AddRange(trabalhos);
            return Task.CompletedTask;
        }
    }
}

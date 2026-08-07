using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Jdice.Application.Campaigns;
using Jdice.Domain.Users;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Jdice.IntegrationTests;

/// <summary>
/// O teste mais importante da fase: um disparo completo, com SMTP de verdade,
/// verificando que a mensagem chegou com o conteúdo certo.
/// <para>
/// Usa um Mailpit em container — servidor que aceita e guarda as mensagens sem
/// entregar a ninguém. Um dublê em memória provaria que o código chama o
/// remetente; só um SMTP real prova que a mensagem sai bem formada.
/// </para>
/// </summary>
public sealed class CampanhaCompletaFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly Testcontainers.PostgreSql.PostgreSqlContainer _postgres =
        new Testcontainers.PostgreSql.PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("jdice_envio")
            .WithUsername("jdice")
            .WithPassword("jdice")
            .Build();

    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:v1.21")
        .WithPortBinding(1025, true)
        .WithPortBinding(8025, true)
        .WithEnvironment("MP_SMTP_AUTH_ACCEPT_ANY", "1")
        .WithEnvironment("MP_SMTP_AUTH_ALLOW_INSECURE", "1")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request =>
            request.ForPort(8025).ForPath("/readyz")))
        .Build();

    public HttpClient MailpitApi { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
                ["Jwt:SigningKey"] = JdiceApiFactory.SigningKey,
                ["Database:AutoMigrate"] = "false",
                ["Seed:AdminEmail"] = "",
                ["Seed:AdminPassword"] = "",
                ["RateLimiting:Login:Enabled"] = "false",
                ["Smtp:Host"] = _mailpit.Hostname,
                ["Smtp:Port"] = _mailpit.GetMappedPublicPort(1025).ToString(),
                ["Smtp:UseStartTls"] = "false",
                ["Smtp:FromEmail"] = "nao-responda@jdice.local",
                ["Smtp:FromName"] = "JDice"
            }));
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();
        await _mailpit.StartAsync();

        MailpitApi = new HttpClient
        {
            BaseAddress = new Uri($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(8025)}")
        };

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<Infrastructure.Persistence.JdiceDbContext>();
        await context.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        MailpitApi?.Dispose();
        await _mailpit.DisposeAsync();
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>Processa o disparo aqui mesmo, no lugar do worker.</summary>
    public async Task ProcessarAsync(Guid campaignId)
    {
        using var scope = Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<CampaignProcessor>();

        await processor.ProcessAsync(campaignId);
    }

    public async Task<IReadOnlyList<MensagemCapturada>> MensagensAsync()
    {
        var json = await MailpitApi.GetStringAsync("/api/v1/messages?limit=200");
        using var documento = JsonDocument.Parse(json);

        var mensagens = new List<MensagemCapturada>();

        foreach (var item in documento.RootElement.GetProperty("messages").EnumerateArray())
        {
            var id = item.GetProperty("ID").GetString()!;
            var corpo = await MailpitApi.GetStringAsync($"/api/v1/message/{id}");

            using var detalhe = JsonDocument.Parse(corpo);

            mensagens.Add(new MensagemCapturada(
                detalhe.RootElement.GetProperty("To")[0].GetProperty("Address").GetString() ?? "",
                detalhe.RootElement.GetProperty("Subject").GetString() ?? "",
                detalhe.RootElement.TryGetProperty("HTML", out var html) ? html.GetString() ?? "" : ""));
        }

        return mensagens;
    }

    public async Task LimparCaixaAsync() =>
        await MailpitApi.DeleteAsync("/api/v1/messages");

    public sealed record MensagemCapturada(string Para, string Assunto, string Html);
}

[CollectionDefinition(nameof(CampanhaCompletaCollection))]
public sealed class CampanhaCompletaCollection : ICollectionFixture<CampanhaCompletaFixture>;

[Collection(nameof(CampanhaCompletaCollection))]
public class CampaignEndToEndTests(CampanhaCompletaFixture factory) : IAsyncLifetime
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

    private static MultipartFormDataContent Csv(string conteudo)
    {
        var arquivo = new ByteArrayContent(new UTF8Encoding(false).GetBytes(conteudo));
        arquivo.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");

        return new MultipartFormDataContent { { arquivo, "arquivo", "lista.csv" } };
    }

    [Fact]
    public async Task Disparo_completo_entrega_email_personalizado_para_cada_pessoa()
    {
        var client = await LogarAsync();

        var modelo = await client.PostAsJsonAsync("/api/templates", new
        {
            nome = "Boas-vindas",
            categoria = "Onboarding",
            tags = new[] { "novo" },
            html = "<p>Olá {{ nome }}, sua empresa {{ empresa }} está no plano {{ plano }}.</p>"
        });
        var template = await modelo.Content.ReadFromJsonAsync<TemplateResponse>();

        var lista = await client.PostAsJsonAsync("/api/recipient-lists", new { nome = "Clientes" });
        var listaId = (await lista.Content.ReadFromJsonAsync<ListaResponse>())!.Id;

        await client.PostAsync($"/api/recipients/import?listaId={listaId}", Csv("""
            email;nome;empresa;plano
            ana@empresa.com;Ana Souza;Acme;Premium
            bruno@empresa.com;Bruno Lima;Beta;Basico
            """));

        var criado = await client.PostAsJsonAsync("/api/campaigns", new
        {
            nome = "Boas-vindas de agosto",
            templateId = template!.Id,
            assunto = "Bem-vindo, {{ nome }}!",
            listaIds = new[] { listaId }
        });

        // 202: o trabalho foi aceito, não concluído. Esperar o envio seria
        // segurar a conexão por minutos num disparo grande.
        Assert.Equal(HttpStatusCode.Accepted, criado.StatusCode);

        var campanha = await criado.Content.ReadFromJsonAsync<CampanhaResponse>();
        Assert.Equal(2, campanha!.Resumo.Total);

        await factory.ProcessarAsync(campanha.Id);

        var mensagens = await factory.MensagensAsync();
        Assert.Equal(2, mensagens.Count);

        var paraAna = Assert.Single(mensagens, m => m.Para == "ana@empresa.com");

        // Cada pessoa recebe o conteúdo montado com os campos dela — é para
        // isso que os campos livres da importação existem.
        Assert.Contains("Ana Souza", paraAna.Html, StringComparison.Ordinal);
        Assert.Contains("Acme", paraAna.Html, StringComparison.Ordinal);
        Assert.Contains("Premium", paraAna.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("Beta", paraAna.Html, StringComparison.Ordinal);

        // O assunto também aceita variáveis.
        Assert.Equal("Bem-vindo, Ana Souza!", paraAna.Assunto);
    }

    [Fact]
    public async Task Processar_duas_vezes_nao_envia_duplicado()
    {
        var client = await LogarAsync();
        var campanhaId = await MontarDisparoSimplesAsync(client);

        await factory.ProcessarAsync(campanhaId);
        await factory.ProcessarAsync(campanhaId);

        // Redelivery de fila, retry e worker reiniciado levam ao mesmo
        // caminho: processar de novo. A pessoa não pode receber duas vezes.
        Assert.Single(await factory.MensagensAsync());
    }

    [Fact]
    public async Task Descadastrado_depois_da_criacao_nao_recebe()
    {
        var client = await LogarAsync();
        var campanhaId = await MontarDisparoSimplesAsync(client);

        var pessoas = await client.GetFromJsonAsync<PaginaResponse>("/api/recipients");
        await client.PostAsync($"/api/recipients/{pessoas!.Itens[0].Id}/unsubscribe", null);

        await factory.ProcessarAsync(campanhaId);

        // Entre criar o disparo e ele sair pode passar tempo; a decisão da
        // pessoa vale até o último instante.
        Assert.Empty(await factory.MensagensAsync());

        var campanha = await client.GetFromJsonAsync<CampanhaResponse>($"/api/campaigns/{campanhaId}");
        Assert.Equal(1, campanha!.Resumo.Pulados);
    }

    [Fact]
    public async Task Disparo_cancelado_nao_envia_nada()
    {
        var client = await LogarAsync();
        var campanhaId = await MontarDisparoSimplesAsync(client, agendarParaOFuturo: true);

        var cancelamento = await client.PostAsync($"/api/campaigns/{campanhaId}/cancel", null);
        Assert.Equal(HttpStatusCode.NoContent, cancelamento.StatusCode);

        // Mesmo que o job dispare por algum motivo, o processador desiste.
        await factory.ProcessarAsync(campanhaId);

        Assert.Empty(await factory.MensagensAsync());
    }

    [Fact]
    public async Task Disparo_em_andamento_nao_pode_ser_cancelado()
    {
        var client = await LogarAsync();
        var campanhaId = await MontarDisparoSimplesAsync(client);

        await factory.ProcessarAsync(campanhaId);

        var tentativa = await client.PostAsync($"/api/campaigns/{campanhaId}/cancel", null);

        // A mensagem já saiu; dizer que cancelou seria mentir para quem operou.
        Assert.Equal(HttpStatusCode.Conflict, tentativa.StatusCode);
    }

    [Fact]
    public async Task Disparo_sem_destinatario_e_recusado()
    {
        var client = await LogarAsync();

        var modelo = await client.PostAsJsonAsync("/api/templates", new
        {
            nome = "Vazio",
            categoria = "",
            tags = Array.Empty<string>(),
            html = "<p>Olá</p>"
        });
        var template = await modelo.Content.ReadFromJsonAsync<TemplateResponse>();

        var resposta = await client.PostAsJsonAsync("/api/campaigns", new
        {
            templateId = template!.Id,
            assunto = "Sem ninguém",
            listaIds = Array.Empty<Guid>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Situacao_por_destinatario_fica_registrada()
    {
        var client = await LogarAsync();
        var campanhaId = await MontarDisparoSimplesAsync(client);

        await factory.ProcessarAsync(campanhaId);

        var entregas = await client.GetFromJsonAsync<List<EntregaResponse>>(
            $"/api/campaigns/{campanhaId}/deliveries");

        var entrega = Assert.Single(entregas!);
        Assert.Equal("Sent", entrega.Situacao);
        Assert.NotNull(entrega.EnviadoEm);
    }

    private async Task<Guid> MontarDisparoSimplesAsync(
        HttpClient client,
        bool agendarParaOFuturo = false)
    {
        var modelo = await client.PostAsJsonAsync("/api/templates", new
        {
            nome = $"Modelo {Guid.CreateVersion7()}",
            categoria = "Teste",
            tags = Array.Empty<string>(),
            html = "<p>Olá {{ nome }}.</p>"
        });
        var template = await modelo.Content.ReadFromJsonAsync<TemplateResponse>();

        var lista = await client.PostAsJsonAsync(
            "/api/recipient-lists",
            new { nome = $"Lista {Guid.CreateVersion7()}" });
        var listaId = (await lista.Content.ReadFromJsonAsync<ListaResponse>())!.Id;

        await client.PostAsync(
            $"/api/recipients/import?listaId={listaId}",
            Csv("email;nome\nana@empresa.com;Ana"));

        var criado = await client.PostAsJsonAsync("/api/campaigns", new
        {
            templateId = template!.Id,
            assunto = "Assunto de teste",
            listaIds = new[] { listaId },
            agendar = agendarParaOFuturo ? DateTimeOffset.UtcNow.AddHours(2) : (DateTimeOffset?)null
        });

        return (await criado.Content.ReadFromJsonAsync<CampanhaResponse>())!.Id;
    }

    private sealed record TemplateResponse(Guid Id);

    private sealed record ListaResponse(Guid Id);

    private sealed record ResumoResponse(
        int Total,
        int Pendentes,
        int Enviando,
        int Enviados,
        int Falhados,
        int Pulados);

    private sealed record CampanhaResponse(Guid Id, string Situacao, ResumoResponse Resumo);

    private sealed record EntregaResponse(
        Guid Id,
        string Email,
        string Situacao,
        int Tentativas,
        string Erro,
        DateTimeOffset? EnviadoEm);

    private sealed record DestinatarioResponse(Guid Id, string Email);

    private sealed record PaginaResponse(IReadOnlyList<DestinatarioResponse> Itens, int Total);
}

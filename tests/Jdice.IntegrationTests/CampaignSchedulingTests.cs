using System.Net;
using System.Net.Http.Json;
using System.Text;
using Jdice.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Jdice.IntegrationTests;

/// <summary>
/// Agendamento: o projeto Java permitia agendar mas não listar, cancelar ou
/// reagendar — você marcava e perdia o controle. Aqui essas três operações
/// existem, e é justamente o que estes testes cobrem.
/// </summary>
[Collection(nameof(CampanhaCompletaCollection))]
public class CampaignSchedulingTests(CampanhaCompletaFixture factory) : IAsyncLifetime
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

    private async Task<(Guid Id, HttpClient Client)> AgendarAsync(DateTimeOffset quando, string fuso = "America/Sao_Paulo")
    {
        var client = await LogarAsync();

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

        await client.PostAsync(
            $"/api/recipients/import?listaId={listaId}",
            Csv("email;nome\nana@empresa.com;Ana"));

        var criado = await client.PostAsJsonAsync("/api/campaigns", new
        {
            templateId,
            assunto = "Agendado",
            listaIds = new[] { listaId },
            agendar = quando,
            fusoHorario = fuso
        });

        var campanha = await criado.Content.ReadFromJsonAsync<CampanhaResponse>();

        return (campanha!.Id, client);
    }

    [Fact]
    public async Task Disparo_agendado_guarda_data_e_fuso()
    {
        var quando = DateTimeOffset.UtcNow.AddDays(1);
        var (id, client) = await AgendarAsync(quando, "America/Manaus");

        var campanha = await client.GetFromJsonAsync<CampanhaResponse>($"/api/campaigns/{id}");

        Assert.Equal("Scheduled", campanha!.Situacao);
        Assert.Equal("America/Manaus", campanha.FusoHorario);

        // Sem guardar o fuso, "amanhã às 9h" perde o sentido quando alguém
        // abre a tela de outro lugar.
        Assert.Equal(quando.ToUnixTimeSeconds(), campanha.AgendadoPara.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task Processar_antes_da_hora_nao_envia()
    {
        var (id, client) = await AgendarAsync(DateTimeOffset.UtcNow.AddDays(1));

        // Chama o processador diretamente, simulando reprocessamento manual ou
        // falha do agendador. E-mail enviado cedo não tem como voltar, então o
        // processador confere a hora mesmo confiando no Hangfire.
        await factory.ProcessarAsync(id);

        Assert.Empty(await factory.MensagensAsync());

        var campanha = await client.GetFromJsonAsync<CampanhaResponse>($"/api/campaigns/{id}");
        Assert.Equal("Scheduled", campanha!.Situacao);
    }

    [Fact]
    public async Task Processar_na_hora_envia_normalmente()
    {
        var (id, _) = await AgendarAsync(DateTimeOffset.UtcNow.AddSeconds(-10));

        await factory.ProcessarAsync(id);

        Assert.Single(await factory.MensagensAsync());
    }

    [Fact]
    public async Task Cancelar_impede_o_envio()
    {
        var (id, client) = await AgendarAsync(DateTimeOffset.UtcNow.AddDays(1));

        var cancelamento = await client.PostAsync($"/api/campaigns/{id}/cancel", null);
        Assert.Equal(HttpStatusCode.NoContent, cancelamento.StatusCode);

        var campanha = await client.GetFromJsonAsync<CampanhaResponse>($"/api/campaigns/{id}");
        Assert.Equal("Cancelled", campanha!.Situacao);

        // Mesmo que o job dispare por algum motivo, o processador desiste.
        await factory.ProcessarAsync(id);
        Assert.Empty(await factory.MensagensAsync());
    }

    [Fact]
    public async Task Reagendar_muda_a_hora_e_o_fuso()
    {
        var (id, client) = await AgendarAsync(DateTimeOffset.UtcNow.AddDays(1));
        var novaData = DateTimeOffset.UtcNow.AddDays(3);

        var resposta = await client.PostAsJsonAsync(
            $"/api/campaigns/{id}/reschedule",
            new { agendar = novaData, fusoHorario = "America/Manaus" });

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);

        var campanha = await client.GetFromJsonAsync<CampanhaResponse>($"/api/campaigns/{id}");

        Assert.Equal(novaData.ToUnixTimeSeconds(), campanha!.AgendadoPara.ToUnixTimeSeconds());
        Assert.Equal("America/Manaus", campanha.FusoHorario);
    }

    [Fact]
    public async Task Reagendar_disparo_ja_processado_e_recusado()
    {
        var (id, client) = await AgendarAsync(DateTimeOffset.UtcNow.AddSeconds(-5));

        await factory.ProcessarAsync(id);

        var resposta = await client.PostAsJsonAsync(
            $"/api/campaigns/{id}/reschedule",
            new { agendar = DateTimeOffset.UtcNow.AddDays(1) });

        // A mensagem já saiu; reagendar daria a impressão de que ela não foi
        // enviada, e quem operou tomaria decisão errada.
        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Fact]
    public async Task Listagem_filtra_por_situacao()
    {
        var (_, client) = await AgendarAsync(DateTimeOffset.UtcNow.AddDays(1));

        var agendados = await client.GetFromJsonAsync<PaginaDeCampanhas>(
            "/api/campaigns?situacao=Scheduled");

        Assert.Equal(1, agendados!.Total);

        var concluidos = await client.GetFromJsonAsync<PaginaDeCampanhas>(
            "/api/campaigns?situacao=Completed");

        Assert.Equal(0, concluidos!.Total);
    }

    [Fact]
    public async Task Cancelar_disparo_inexistente_devolve_404()
    {
        var client = await LogarAsync();

        var resposta = await client.PostAsync(
            $"/api/campaigns/{Guid.CreateVersion7()}/cancel",
            null);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    private sealed record IdResponse(Guid Id);

    private sealed record CampanhaResponse(
        Guid Id,
        string Situacao,
        DateTimeOffset AgendadoPara,
        string FusoHorario);

    private sealed record PaginaDeCampanhas(IReadOnlyList<CampanhaResponse> Itens, int Total);
}

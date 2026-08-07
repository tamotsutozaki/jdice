using System.Net;
using System.Net.Http.Json;
using System.Text;
using Jdice.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Jdice.IntegrationTests;

/// <summary>
/// O painel do projeto original mostrava "taxa de abertura ~68%" e "735
/// aberturas estimadas" escritos no código-fonte. Estes testes existem para
/// garantir o oposto: todo número da tela sai de uma contagem no banco, e muda
/// quando o sistema muda.
/// </summary>
[Collection(nameof(CampanhaCompletaCollection))]
public class DashboardEndpointsTests(CampanhaCompletaFixture factory) : IAsyncLifetime
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
    public async Task Sem_login_o_painel_nao_abre()
    {
        var anonimo = factory.CreateClient();

        var resposta = await anonimo.GetAsync("/api/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Sistema_recem_instalado_mostra_tudo_zerado()
    {
        var client = await LogarAsync();

        var painel = await client.GetFromJsonAsync<PainelResponse>("/api/dashboard");

        // Zero é a resposta honesta de um sistema sem uso. O painel antigo
        // exibia números cheios mesmo com o banco vazio.
        Assert.Equal(0, painel!.Modelos);
        Assert.Equal(0, painel.Destinatarios);
        Assert.Equal(0, painel.EmailsEnviados);
        Assert.Empty(painel.Recentes);
    }

    [Fact]
    public async Task Contagens_acompanham_o_que_foi_cadastrado_e_enviado()
    {
        var client = await LogarAsync();
        var campanhaId = await MontarDisparoAsync(client, "Boas-vindas de agosto");

        var antes = await client.GetFromJsonAsync<PainelResponse>("/api/dashboard");

        Assert.Equal(1, antes!.Modelos);
        Assert.Equal(2, antes.Destinatarios);
        Assert.Equal(1, antes.Listas);
        Assert.Equal(0, antes.EmailsEnviados);

        await factory.ProcessarAsync(campanhaId);

        var depois = await client.GetFromJsonAsync<PainelResponse>("/api/dashboard");

        Assert.Equal(2, depois!.EmailsEnviados);
        Assert.Equal(2, depois.EnviadosNosUltimos30Dias);
    }

    [Fact]
    public async Task Descadastrado_sai_da_contagem_de_destinatarios()
    {
        var client = await LogarAsync();
        await MontarDisparoAsync(client, "Qualquer");

        var pessoas = await client.GetFromJsonAsync<PaginaDeDestinatarios>("/api/recipients");
        await client.PostAsync($"/api/recipients/{pessoas!.Itens[0].Id}/unsubscribe", null);

        var painel = await client.GetFromJsonAsync<PainelResponse>("/api/dashboard");

        // Quem pediu para sair não é alcançável; contar essa pessoa infla o
        // número que decide o tamanho de um disparo.
        Assert.Equal(1, painel!.Destinatarios);
    }

    [Fact]
    public async Task Agendado_aparece_na_contagem_e_na_lista_de_recentes()
    {
        var client = await LogarAsync();
        var campanhaId = await MontarDisparoAsync(
            client,
            "Comunicado de setembro",
            agendarPara: DateTimeOffset.UtcNow.AddDays(2));

        var painel = await client.GetFromJsonAsync<PainelResponse>("/api/dashboard");

        Assert.Equal(1, painel!.DisparosAgendados);

        var recente = Assert.Single(painel.Recentes);
        Assert.Equal(campanhaId, recente.Id);
        Assert.Equal("Comunicado de setembro", recente.Nome);
        Assert.Equal("Scheduled", recente.Situacao);
        Assert.Equal(2, recente.Total);
        Assert.Equal(0, recente.Enviados);
    }

    [Fact]
    public async Task Recentes_traz_no_maximo_cinco_do_mais_novo_para_o_mais_antigo()
    {
        var client = await LogarAsync();

        for (var i = 1; i <= 6; i++)
        {
            await MontarDisparoAsync(
                client,
                $"Disparo {i}",
                agendarPara: DateTimeOffset.UtcNow.AddDays(i));
        }

        var painel = await client.GetFromJsonAsync<PainelResponse>("/api/dashboard");

        Assert.Equal(5, painel!.Recentes.Count);
        Assert.Equal("Disparo 6", painel.Recentes[0].Nome);
        Assert.Equal("Disparo 2", painel.Recentes[4].Nome);
    }

    private async Task<Guid> MontarDisparoAsync(
        HttpClient client,
        string nome,
        DateTimeOffset? agendarPara = null)
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

        await client.PostAsync($"/api/recipients/import?listaId={listaId}", Csv("""
            email;nome
            ana@empresa.com;Ana
            bruno@empresa.com;Bruno
            """));

        var criado = await client.PostAsJsonAsync("/api/campaigns", new
        {
            nome,
            templateId,
            assunto = "Assunto de teste",
            listaIds = new[] { listaId },
            agendar = agendarPara
        });

        return (await criado.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record DestinatarioResponse(Guid Id, string Email);

    private sealed record PaginaDeDestinatarios(IReadOnlyList<DestinatarioResponse> Itens);

    private sealed record DisparoRecente(
        Guid Id,
        string Nome,
        string Situacao,
        int Total,
        int Enviados,
        DateTimeOffset AgendadoPara);

    private sealed record PainelResponse(
        int Modelos,
        int Destinatarios,
        int Listas,
        int DisparosAgendados,
        int EmailsEnviados,
        int EnviadosNosUltimos30Dias,
        IReadOnlyList<DisparoRecente> Recentes);
}

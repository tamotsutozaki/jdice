using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Jdice.Domain.Users;

namespace Jdice.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class RecipientEndpointsTests(JdiceApiFactory factory) : IAsyncLifetime
{
    private const string Senha = "senha-bem-comprida-123";

    public async Task InitializeAsync()
    {
        await factory.ResetRecipientsAsync();
        await factory.ResetUsersAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> LogarAsync(
        string email = "comum@empresa.com",
        UserRole role = UserRole.User)
    {
        await factory.CriarUsuarioAsync(email, Senha, role);

        var client = factory.CreateClient(new() { HandleCookies = true });
        await client.PostAsJsonAsync("/api/auth/login", new { email, senha = Senha });

        return client;
    }

    private static MultipartFormDataContent ArquivoCsv(string conteudo, Encoding? codificacao = null)
    {
        var bytes = (codificacao ?? new UTF8Encoding(false)).GetBytes(conteudo);
        var arquivo = new ByteArrayContent(bytes);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("text/csv");

        return new MultipartFormDataContent { { arquivo, "arquivo", "destinatarios.csv" } };
    }

    private static async Task<Guid> CriarListaAsync(HttpClient client, string nome = "Clientes")
    {
        var resposta = await client.PostAsJsonAsync("/api/recipient-lists", new { nome });
        var lista = await resposta.Content.ReadFromJsonAsync<ListaResponse>();

        return lista!.Id;
    }

    [Fact]
    public async Task Cadastro_manual_cria_destinatario()
    {
        var client = await LogarAsync();

        var resposta = await client.PostAsJsonAsync("/api/recipients", new
        {
            email = "Ana@Empresa.com",
            nome = "Ana",
            campos = new Dictionary<string, string> { ["empresa"] = "Acme" }
        });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        var criado = await resposta.Content.ReadFromJsonAsync<RecipienteResponse>();
        Assert.Equal("ana@empresa.com", criado!.Email);
        Assert.Equal("Acme", criado.Campos["empresa"]);
        Assert.True(criado.Inscrito);
    }

    [Fact]
    public async Task Email_repetido_e_recusado()
    {
        var client = await LogarAsync();

        await client.PostAsJsonAsync("/api/recipients", new { email = "ana@empresa.com" });
        var segunda = await client.PostAsJsonAsync("/api/recipients", new { email = "ANA@EMPRESA.COM" });

        // Mesma pessoa em caixa diferente continua sendo a mesma pessoa.
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
    }

    [Fact]
    public async Task Importacao_traz_o_que_da_e_relata_o_resto()
    {
        var client = await LogarAsync();

        var resposta = await client.PostAsync("/api/recipients/import", ArquivoCsv("""
            email;nome;empresa
            ana@empresa.com;Ana;Acme
            sem-arroba;Bruno;Beta
            carla@empresa.com;Carla;Gama
            ;Daniel;Delta
            """));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var resultado = await resposta.Content.ReadFromJsonAsync<ImportacaoResponse>();

        // Recusar as três boas por causa das duas ruins faria a pessoa
        // procurar o erro no escuro.
        Assert.Equal(2, resultado!.Criados);
        Assert.Equal(2, resultado.Recusados.Count);
        Assert.Contains(resultado.Recusados, r => r.Linha == 3);
        Assert.Contains(resultado.Recusados, r => r.Linha == 5);
    }

    [Fact]
    public async Task Importacao_le_acentuacao_do_excel()
    {
        var client = await LogarAsync();

        // Excel em português salva em Windows-1252.
        var conteudo = "email;nome\njoao@empresa.com;João Conceição";
        var resposta = await client.PostAsync(
            "/api/recipients/import",
            ArquivoCsv(conteudo, Encoding.GetEncoding(1252)));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var lista = await client.GetFromJsonAsync<PaginaResponse>("/api/recipients");
        Assert.Equal("João Conceição", Assert.Single(lista!.Itens).Nome);
    }

    [Fact]
    public async Task Importacao_atualiza_quem_ja_existe_em_vez_de_duplicar()
    {
        var client = await LogarAsync();

        await client.PostAsync("/api/recipients/import", ArquivoCsv("""
            email;nome;empresa
            ana@empresa.com;Ana;Acme
            """));

        var segunda = await client.PostAsync("/api/recipients/import", ArquivoCsv("""
            email;nome;plano
            ana@empresa.com;Ana Souza;Premium
            """));

        var resultado = await segunda.Content.ReadFromJsonAsync<ImportacaoResponse>();
        Assert.Equal(0, resultado!.Criados);
        Assert.Equal(1, resultado.Atualizados);

        var lista = await client.GetFromJsonAsync<PaginaResponse>("/api/recipients");
        var ana = Assert.Single(lista!.Itens);

        Assert.Equal("Ana Souza", ana.Nome);
        Assert.Equal("Premium", ana.Campos["plano"]);

        // A empresa da primeira planilha não pode ter sido apagada pela segunda.
        Assert.Equal("Acme", ana.Campos["empresa"]);
    }

    [Fact]
    public async Task Importacao_com_lista_inclui_todo_mundo_nela()
    {
        var client = await LogarAsync();
        var listaId = await CriarListaAsync(client);

        await client.PostAsync($"/api/recipients/import?listaId={listaId}", ArquivoCsv("""
            email;nome
            ana@empresa.com;Ana
            bruno@empresa.com;Bruno
            """));

        var naLista = await client.GetFromJsonAsync<PaginaResponse>(
            $"/api/recipients?listaId={listaId}");

        Assert.Equal(2, naLista!.Total);
    }

    [Fact]
    public async Task Importacao_em_lista_inexistente_devolve_404()
    {
        var client = await LogarAsync();

        var resposta = await client.PostAsync(
            $"/api/recipients/import?listaId={Guid.CreateVersion7()}",
            ArquivoCsv("email\nana@empresa.com"));

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task Arquivo_sem_coluna_de_email_volta_com_explicacao()
    {
        var client = await LogarAsync();

        var resposta = await client.PostAsync("/api/recipients/import", ArquivoCsv("""
            nome;empresa
            Ana;Acme
            """));

        // Processar e explicar é melhor que 400 seco: a pessoa precisa saber
        // o que corrigir no arquivo.
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var resultado = await resposta.Content.ReadFromJsonAsync<ImportacaoResponse>();
        Assert.Equal(0, resultado!.Importados);
        Assert.Contains("email", Assert.Single(resultado.Recusados).Motivo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Descadastro_vale_para_todas_as_listas()
    {
        var client = await LogarAsync();

        var primeira = await CriarListaAsync(client, "Newsletter");
        var segunda = await CriarListaAsync(client, "Promoções");

        var criado = await client.PostAsJsonAsync(
            "/api/recipients",
            new { email = "ana@empresa.com", listaId = primeira });
        var ana = await criado.Content.ReadFromJsonAsync<RecipienteResponse>();

        await client.PostAsync($"/api/recipient-lists/{segunda}/members/{ana!.Id}", null);

        await client.PostAsync($"/api/recipients/{ana.Id}/unsubscribe", null);

        // Quem pede para sair espera parar de receber, não parar só na lista
        // de onde veio a mensagem.
        foreach (var listaId in new[] { primeira, segunda })
        {
            var naLista = await client.GetFromJsonAsync<PaginaResponse>(
                $"/api/recipients?listaId={listaId}");

            Assert.Equal(0, naLista!.Total);
        }
    }

    [Fact]
    public async Task Descadastrado_reaparece_quando_pedido()
    {
        var client = await LogarAsync();

        var criado = await client.PostAsJsonAsync("/api/recipients", new { email = "ana@empresa.com" });
        var ana = await criado.Content.ReadFromJsonAsync<RecipienteResponse>();

        await client.PostAsync($"/api/recipients/{ana!.Id}/unsubscribe", null);

        var padrao = await client.GetFromJsonAsync<PaginaResponse>("/api/recipients");
        Assert.Equal(0, padrao!.Total);

        var comDescadastrados = await client.GetFromJsonAsync<PaginaResponse>(
            "/api/recipients?incluirDescadastrados=true");
        Assert.Equal(1, comDescadastrados!.Total);
        Assert.False(comDescadastrados.Itens[0].Inscrito);
    }

    [Fact]
    public async Task Sair_da_lista_nao_descadastra_a_pessoa()
    {
        var client = await LogarAsync();
        var listaId = await CriarListaAsync(client);

        var criado = await client.PostAsJsonAsync(
            "/api/recipients",
            new { email = "ana@empresa.com", listaId });
        var ana = await criado.Content.ReadFromJsonAsync<RecipienteResponse>();

        await client.DeleteAsync($"/api/recipient-lists/{listaId}/members/{ana!.Id}");

        // Sair de um agrupamento é diferente de pedir para não receber nada.
        var todos = await client.GetFromJsonAsync<PaginaResponse>("/api/recipients");
        Assert.Equal(1, todos!.Total);
        Assert.True(todos.Itens[0].Inscrito);
    }

    [Fact]
    public async Task Listagem_e_paginada()
    {
        var client = await LogarAsync();

        var linhas = string.Join(
            "\n",
            Enumerable.Range(1, 25).Select(i => $"pessoa{i:D2}@empresa.com;Pessoa {i}"));

        await client.PostAsync("/api/recipients/import", ArquivoCsv($"email;nome\n{linhas}"));

        var primeira = await client.GetFromJsonAsync<PaginaResponse>("/api/recipients?pagina=1&tamanho=10");

        Assert.Equal(25, primeira!.Total);
        Assert.Equal(10, primeira.Itens.Count);
        Assert.Equal(3, primeira.TotalDePaginas);
    }

    [Fact]
    public async Task Busca_encontra_por_email_e_por_nome()
    {
        var client = await LogarAsync();

        await client.PostAsync("/api/recipients/import", ArquivoCsv("""
            email;nome
            ana@empresa.com;Ana Souza
            bruno@outra.com;Bruno Lima
            """));

        var porEmail = await client.GetFromJsonAsync<PaginaResponse>("/api/recipients?busca=outra");
        Assert.Equal(1, porEmail!.Total);

        var porNome = await client.GetFromJsonAsync<PaginaResponse>("/api/recipients?busca=souza");
        Assert.Equal(1, porNome!.Total);
    }

    [Fact]
    public async Task Lista_traz_contagem_de_membros()
    {
        var client = await LogarAsync();
        var listaId = await CriarListaAsync(client);

        await client.PostAsync($"/api/recipients/import?listaId={listaId}", ArquivoCsv("""
            email;nome
            ana@empresa.com;Ana
            bruno@empresa.com;Bruno
            """));

        var listas = await client.GetFromJsonAsync<List<ListaResponse>>("/api/recipient-lists");
        var lista = Assert.Single(listas!);

        Assert.Equal(2, lista.TotalDeMembros);
        Assert.Equal(2, lista.TotalAtivos);
    }

    [Fact]
    public async Task Usuario_comum_nao_remove_destinatario()
    {
        var admin = await LogarAsync("admin@empresa.com", UserRole.Admin);
        var criado = await admin.PostAsJsonAsync("/api/recipients", new { email = "ana@empresa.com" });
        var ana = await criado.Content.ReadFromJsonAsync<RecipienteResponse>();

        var comum = await LogarAsync();
        var tentativa = await comum.DeleteAsync($"/api/recipients/{ana!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, tentativa.StatusCode);
    }

    [Fact]
    public async Task Visitante_nao_acessa_destinatarios()
    {
        var resposta = await factory.CreateClient().GetAsync("/api/recipients");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    private sealed record RecipienteResponse(
        Guid Id,
        string Email,
        string Nome,
        IReadOnlyDictionary<string, string> Campos,
        bool Inscrito);

    private sealed record PaginaResponse(
        IReadOnlyList<RecipienteResponse> Itens,
        int Total,
        int Pagina,
        int TamanhoDaPagina,
        int TotalDePaginas);

    private sealed record ListaResponse(
        Guid Id,
        string Nome,
        string Descricao,
        int TotalDeMembros,
        int TotalAtivos);

    private sealed record ImportacaoResponse(
        int TotalDeLinhas,
        int Importados,
        int Criados,
        int Atualizados,
        int JaNaLista,
        IReadOnlyList<ImportIssueResponse> Recusados,
        IReadOnlyList<string> ColunasLivres);

    private sealed record ImportIssueResponse(int Linha, string Motivo);
}

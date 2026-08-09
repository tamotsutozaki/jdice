using System.Net;
using System.Net.Http.Json;
using Jdice.Domain.Users;

namespace Jdice.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class TemplateEndpointsTests(JdiceApiFactory factory) : IAsyncLifetime
{
    private const string Senha = "senha-bem-comprida-123";
    private const string HtmlSimples = "<p>Olá {{ nome }}, seu pedido {{ numero }}.</p>";

    public async Task InitializeAsync()
    {
        await factory.ResetTemplatesAsync();
        await factory.ResetUsersAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> LogarAsync(string email, UserRole role)
    {
        await factory.CriarUsuarioAsync(email, Senha, role);

        var client = factory.CreateClient(new() { HandleCookies = true });
        await client.PostAsJsonAsync("/api/auth/login", new { email, senha = Senha });

        return client;
    }

    private static object NovoModelo(
        string nome = "Boas-vindas",
        string categoria = "Onboarding",
        string html = HtmlSimples,
        string[]? tags = null) =>
        new { nome, categoria, tags = tags ?? ["novo", "cliente"], html };

    [Fact]
    public async Task Criar_modelo_gera_a_primeira_versao_com_as_variaveis()
    {
        var client = await LogarAsync("comum@empresa.com", UserRole.User);

        var resposta = await client.PostAsJsonAsync("/api/templates", NovoModelo());

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        var detalhe = await resposta.Content.ReadFromJsonAsync<Detalhe>();
        Assert.NotNull(detalhe);

        var versao = Assert.Single(detalhe.Versoes);
        Assert.Equal(1, versao.Numero);
        Assert.Equal(["nome", "numero"], versao.Variaveis);
    }

    [Fact]
    public async Task Usuario_comum_pode_criar_modelo()
    {
        var client = await LogarAsync("comum@empresa.com", UserRole.User);

        // Criar é o trabalho diário e não depende de admin.
        var resposta = await client.PostAsJsonAsync("/api/templates", NovoModelo());

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
    }

    [Fact]
    public async Task Conteudo_com_erro_de_sintaxe_e_recusado()
    {
        var client = await LogarAsync("comum@empresa.com", UserRole.User);

        var resposta = await client.PostAsJsonAsync(
            "/api/templates",
            NovoModelo(html: "{{ if vip }}sem fechar"));

        // Guardar um modelo que nunca renderiza seria pior ainda porque a
        // versão, uma vez criada, não pode ser corrigida.
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Nome_repetido_e_recusado()
    {
        var client = await LogarAsync("comum@empresa.com", UserRole.User);

        await client.PostAsJsonAsync("/api/templates", NovoModelo());
        var segunda = await client.PostAsJsonAsync("/api/templates", NovoModelo());

        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
    }

    [Fact]
    public async Task Nova_versao_incrementa_o_numero_e_preserva_a_anterior()
    {
        var client = await LogarAsync("comum@empresa.com", UserRole.User);

        var criado = await client.PostAsJsonAsync("/api/templates", NovoModelo());
        var detalhe = await criado.Content.ReadFromJsonAsync<Detalhe>();

        var novaVersao = await client.PostAsJsonAsync(
            $"/api/templates/{detalhe!.Id}/versions",
            new { html = "<p>Olá {{ nome }}, versão nova.</p>" });

        Assert.Equal(HttpStatusCode.Created, novaVersao.StatusCode);

        var atualizado = await client.GetFromJsonAsync<Detalhe>($"/api/templates/{detalhe.Id}");

        Assert.Equal(2, atualizado!.Versoes.Count);

        // A versão 1 continua exatamente como foi criada: é isso que torna
        // possível responder "o que exatamente foi disparado em julho?".
        var primeira = atualizado.Versoes.Single(versao => versao.Numero == 1);
        Assert.Equal(HtmlSimples, primeira.Html);
    }

    [Fact]
    public async Task Nao_existe_rota_para_alterar_o_conteudo_de_uma_versao()
    {
        var client = await LogarAsync("comum@empresa.com", UserRole.User);

        var criado = await client.PostAsJsonAsync("/api/templates", NovoModelo());
        var detalhe = await criado.Content.ReadFromJsonAsync<Detalhe>();
        var versaoId = detalhe!.Versoes[0].Id;

        // A imutabilidade é a razão de existir do versionamento: se um dia
        // alguém adicionar uma rota dessas, este teste avisa.
        var put = await client.PutAsJsonAsync(
            $"/api/templates/{detalhe.Id}/versions/{versaoId}",
            new { html = "<p>tentativa de sobrescrever</p>" });

        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);

        var intacto = await client.GetFromJsonAsync<Detalhe>($"/api/templates/{detalhe.Id}");
        Assert.Equal(HtmlSimples, intacto!.Versoes[0].Html);
    }

    [Fact]
    public async Task Editar_metadados_nao_cria_versao()
    {
        var client = await LogarAsync("comum@empresa.com", UserRole.User);

        var criado = await client.PostAsJsonAsync("/api/templates", NovoModelo());
        var detalhe = await criado.Content.ReadFromJsonAsync<Detalhe>();

        var atualizado = await client.PutAsJsonAsync(
            $"/api/templates/{detalhe!.Id}",
            new { nome = "Boas-vindas revisado", categoria = "Marketing", tags = new[] { "corrigida" } });

        Assert.Equal(HttpStatusCode.OK, atualizado.StatusCode);

        var depois = await atualizado.Content.ReadFromJsonAsync<Detalhe>();

        // Corrigir uma tag não pode gerar versão de HTML idêntica à anterior.
        Assert.Single(depois!.Versoes);
        Assert.Equal("Boas-vindas revisado", depois.Nome);
        Assert.Equal("Marketing", depois.Categoria);
    }

    [Fact]
    public async Task Usuario_comum_nao_arquiva()
    {
        var admin = await LogarAsync("admin@empresa.com", UserRole.Admin);
        var criado = await admin.PostAsJsonAsync("/api/templates", NovoModelo());
        var detalhe = await criado.Content.ReadFromJsonAsync<Detalhe>();

        var comum = await LogarAsync("comum@empresa.com", UserRole.User);
        var tentativa = await comum.DeleteAsync($"/api/templates/{detalhe!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, tentativa.StatusCode);
    }

    [Fact]
    public async Task Admin_arquiva_e_o_modelo_some_da_listagem()
    {
        var admin = await LogarAsync("admin@empresa.com", UserRole.Admin);
        var criado = await admin.PostAsJsonAsync("/api/templates", NovoModelo());
        var detalhe = await criado.Content.ReadFromJsonAsync<Detalhe>();

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await admin.DeleteAsync($"/api/templates/{detalhe!.Id}")).StatusCode);

        var listaPadrao = await admin.GetFromJsonAsync<List<ItemDeLista>>("/api/templates");
        Assert.Empty(listaPadrao!);

        // Arquivar não apaga: as versões continuam lá para explicar disparos antigos.
        var comArquivados = await admin.GetFromJsonAsync<List<ItemDeLista>>(
            "/api/templates?incluirArquivados=true");
        Assert.Single(comArquivados!);
        Assert.True(comArquivados![0].Arquivado);
    }

    [Fact]
    public async Task Modelo_arquivado_nao_recebe_nova_versao()
    {
        var admin = await LogarAsync("admin@empresa.com", UserRole.Admin);
        var criado = await admin.PostAsJsonAsync("/api/templates", NovoModelo());
        var detalhe = await criado.Content.ReadFromJsonAsync<Detalhe>();

        await admin.DeleteAsync($"/api/templates/{detalhe!.Id}");

        var tentativa = await admin.PostAsJsonAsync(
            $"/api/templates/{detalhe.Id}/versions",
            new { html = "<p>nova</p>" });

        Assert.Equal(HttpStatusCode.Conflict, tentativa.StatusCode);
    }

    [Fact]
    public async Task Desarquivar_devolve_o_modelo_a_listagem()
    {
        var admin = await LogarAsync("admin@empresa.com", UserRole.Admin);
        var criado = await admin.PostAsJsonAsync("/api/templates", NovoModelo());
        var detalhe = await criado.Content.ReadFromJsonAsync<Detalhe>();

        await admin.DeleteAsync($"/api/templates/{detalhe!.Id}");
        await admin.PostAsync($"/api/templates/{detalhe.Id}/unarchive", null);

        var lista = await admin.GetFromJsonAsync<List<ItemDeLista>>("/api/templates");
        Assert.Single(lista!);
    }

    [Fact]
    public async Task Busca_filtra_por_nome_sem_distinguir_caixa()
    {
        var client = await LogarAsync("comum@empresa.com", UserRole.User);

        await client.PostAsJsonAsync("/api/templates", NovoModelo(nome: "Boas-vindas"));
        await client.PostAsJsonAsync("/api/templates", NovoModelo(nome: "Cobrança mensal"));

        var resultado = await client.GetFromJsonAsync<List<ItemDeLista>>("/api/templates?busca=BOAS");

        Assert.Single(resultado!);
        Assert.Equal("Boas-vindas", resultado![0].Nome);
    }

    [Fact]
    public async Task Filtra_por_tag()
    {
        var client = await LogarAsync("comum@empresa.com", UserRole.User);

        await client.PostAsJsonAsync("/api/templates", NovoModelo(nome: "A", tags: ["promo"]));
        await client.PostAsJsonAsync("/api/templates", NovoModelo(nome: "B", tags: ["aviso"]));

        var resultado = await client.GetFromJsonAsync<List<ItemDeLista>>("/api/templates?tag=promo");

        Assert.Single(resultado!);
        Assert.Equal("A", resultado![0].Nome);
    }

    [Fact]
    public async Task Tags_lista_as_usadas_sem_repetir_e_em_ordem()
    {
        var client = await LogarAsync("comum@empresa.com", UserRole.User);

        await client.PostAsJsonAsync("/api/templates", NovoModelo(nome: "A", tags: ["promo", "vip"]));
        await client.PostAsJsonAsync("/api/templates", NovoModelo(nome: "B", tags: ["aviso", "promo"]));

        var tags = await client.GetFromJsonAsync<List<string>>("/api/templates/tags");

        // O filtro por tag precisa de uma fonte de tags; sem repetir "promo" e
        // em ordem, para virar um seletor previsível na interface.
        Assert.Equal(["aviso", "promo", "vip"], tags);
    }

    [Fact]
    public async Task Preview_renderiza_com_os_valores_informados()
    {
        var client = await LogarAsync("comum@empresa.com", UserRole.User);

        var resposta = await client.PostAsJsonAsync("/api/templates/preview", new
        {
            html = HtmlSimples,
            valores = new Dictionary<string, string> { ["nome"] = "Pedro", ["numero"] = "42" }
        });

        var preview = await resposta.Content.ReadFromJsonAsync<Preview>();

        Assert.Equal("<p>Olá Pedro, seu pedido 42.</p>", preview!.Html);
        Assert.Empty(preview.Erros);
    }

    [Fact]
    public async Task Preview_de_conteudo_quebrado_devolve_erro_sem_falhar_a_requisicao()
    {
        var client = await LogarAsync("comum@empresa.com", UserRole.User);

        var resposta = await client.PostAsJsonAsync("/api/templates/preview", new
        {
            html = "{{ if vip }}sem fechar",
            valores = new Dictionary<string, string>()
        });

        // Quem está escrevendo precisa ver o erro junto do preview, não um 400 seco.
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var preview = await resposta.Content.ReadFromJsonAsync<Preview>();
        Assert.Null(preview!.Html);
        Assert.NotEmpty(preview.Erros);
    }

    [Fact]
    public async Task Analisar_conteudo_vazio_devolve_lista_vazia_e_nao_erro()
    {
        var client = await LogarAsync("comum@empresa.com", UserRole.User);

        // O editor consulta enquanto a pessoa escreve, e a primeira consulta
        // acontece com o campo ainda em branco. Recusar isso com 400 fazia a
        // tela inteira parar de analisar depois da primeira requisição.
        var resposta = await client.PostAsJsonAsync(
            "/api/templates/analyze",
            new { html = "", valores = new Dictionary<string, string>() });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var analise = await resposta.Content.ReadFromJsonAsync<Analise>();
        Assert.Empty(analise!.Variaveis);
        Assert.Empty(analise.Erros);
    }

    [Fact]
    public async Task Preview_de_conteudo_vazio_nao_e_erro()
    {
        var client = await LogarAsync("comum@empresa.com", UserRole.User);

        var resposta = await client.PostAsJsonAsync(
            "/api/templates/preview",
            new { html = "", valores = new Dictionary<string, string>() });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task Analisar_encontra_variavel_dentro_de_condicional_e_ignora_item_de_laco()
    {
        var client = await LogarAsync("comum@empresa.com", UserRole.User);

        var resposta = await client.PostAsJsonAsync("/api/templates/analyze", new
        {
            html = "{{ if vip }}Olá {{ nome }}{{ end }}"
                + "{{ for item in produtos }}<li>{{ item }}</li>{{ end }}",
            valores = new Dictionary<string, string>()
        });

        var analise = await resposta.Content.ReadFromJsonAsync<Analise>();

        // É o caminho que a interface usa para listar o que precisa ser
        // preenchido: "vip" e "produtos" vêm de fora, "item" é da iteração.
        Assert.Equal(["vip", "nome", "produtos"], analise!.Variaveis);
    }

    [Fact]
    public async Task Visitante_nao_acessa_modelos()
    {
        var client = factory.CreateClient();

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/templates")).StatusCode);
    }

    private sealed record Detalhe(
        Guid Id,
        string Nome,
        string Categoria,
        IReadOnlyList<string> Tags,
        bool Arquivado,
        IReadOnlyList<Versao> Versoes);

    private sealed record Versao(Guid Id, int Numero, string Html, IReadOnlyList<string> Variaveis);

    private sealed record ItemDeLista(
        Guid Id,
        string Nome,
        string Categoria,
        IReadOnlyList<string> Tags,
        int TotalDeVersoes,
        int? VersaoAtual,
        bool Arquivado);

    private sealed record Preview(string? Html, IReadOnlyList<string> Erros);

    private sealed record Analise(IReadOnlyList<string> Variaveis, IReadOnlyList<string> Erros);
}

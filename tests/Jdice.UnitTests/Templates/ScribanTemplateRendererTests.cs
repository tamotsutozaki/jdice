using Jdice.Infrastructure.Templates;

namespace Jdice.UnitTests.Templates;

/// <summary>
/// A renderização é a regra de negócio central do sistema: é ela que
/// transforma um modelo guardado no e-mail que a pessoa recebe. Entrada de
/// texto e saída de texto, sem I/O — o tipo de coisa que compensa cobrir bem.
/// </summary>
public class ScribanTemplateRendererTests
{
    private readonly ScribanTemplateRenderer _renderer = new();

    private static Dictionary<string, string?> Valores(params (string Nome, string? Valor)[] pares) =>
        pares.ToDictionary(par => par.Nome, par => par.Valor);

    [Fact]
    public void Substitui_variavel_simples()
    {
        var resultado = _renderer.Render(
            "<p>Olá {{ nome }}, tudo bem?</p>",
            Valores(("nome", "Pedro")));

        Assert.True(resultado.Succeeded);
        Assert.Equal("<p>Olá Pedro, tudo bem?</p>", resultado.Html);
    }

    [Fact]
    public void Substitui_a_mesma_variavel_em_varios_lugares()
    {
        var resultado = _renderer.Render(
            "{{ nome }} — obrigado, {{ nome }}!",
            Valores(("nome", "Pedro")));

        Assert.Equal("Pedro — obrigado, Pedro!", resultado.Html);
    }

    [Fact]
    public void Variavel_sem_valor_vira_vazio()
    {
        var resultado = _renderer.Render("Olá {{ nome }}.", Valores(("nome", null)));

        // Imprimir "null" no corpo do e-mail seria pior que deixar em branco.
        Assert.True(resultado.Succeeded);
        Assert.Equal("Olá .", resultado.Html);
    }

    [Fact]
    public void Variavel_nao_informada_nao_derruba_a_renderizacao()
    {
        var resultado = _renderer.Render("Olá {{ desconhecida }}.", Valores());

        Assert.True(resultado.Succeeded);
        Assert.Equal("Olá .", resultado.Html);
    }

    [Fact]
    public void Extrai_variaveis_na_ordem_em_que_aparecem()
    {
        var analise = _renderer.Analyze("<p>{{ saudacao }} {{ nome }}, seu pedido {{ numero }}.</p>");

        Assert.True(analise.IsValid);
        Assert.Equal(["saudacao", "nome", "numero"], analise.Variables);
    }

    [Fact]
    public void Variavel_repetida_aparece_uma_vez_so()
    {
        var analise = _renderer.Analyze("{{ nome }} {{ nome }} {{ nome }}");

        Assert.Equal(["nome"], analise.Variables);
    }

    [Fact]
    public void Encontra_variavel_dentro_de_condicional()
    {
        var analise = _renderer.Analyze("{{ if vip }}Bem-vindo, {{ nome }}!{{ end }}");

        // Uma expressão regular sobre o texto acharia "nome", mas não "vip" —
        // e é justamente "vip" que quem dispara precisa preencher.
        Assert.Contains("vip", analise.Variables);
        Assert.Contains("nome", analise.Variables);
    }

    [Fact]
    public void Item_de_laco_nao_conta_como_variavel_a_preencher()
    {
        var analise = _renderer.Analyze("{{ for item in produtos }}<li>{{ item }}</li>{{ end }}");

        // "produtos" vem de fora; "item" existe só dentro da iteração.
        Assert.Contains("produtos", analise.Variables);
        Assert.DoesNotContain("item", analise.Variables);
    }

    [Fact]
    public void Chaves_soltas_no_texto_nao_viram_variavel()
    {
        var analise = _renderer.Analyze("<p>Use { chaves } normalmente, e {{ nome }} como variável.</p>");

        Assert.Equal(["nome"], analise.Variables);
    }

    [Fact]
    public void Bloco_vazio_e_aceito_e_nao_produz_saida()
    {
        // O motor trata "{{ }}" como bloco sem expressão, não como erro — e de
        // fato não há por que recusar algo que apenas não imprime nada.
        var resultado = _renderer.Render("<p>a{{ }}b</p>", Valores());

        Assert.True(resultado.Succeeded);
        Assert.Equal("<p>ab</p>", resultado.Html);
    }

    [Fact]
    public void Modelo_sem_variavel_nenhuma_e_valido()
    {
        var analise = _renderer.Analyze("<p>Comunicado geral, sem personalização.</p>");

        Assert.True(analise.IsValid);
        Assert.Empty(analise.Variables);
    }

    [Theory]
    [InlineData("{{ if vip }}sem fechar")]
    [InlineData("{{ for x in y }}sem end")]
    [InlineData("{{ nome + }}")]
    public void Sintaxe_invalida_vira_erro_e_nao_excecao(string htmlQuebrado)
    {
        var analise = _renderer.Analyze(htmlQuebrado);

        // Conteúdo escrito por quem usa o sistema não pode derrubar a
        // requisição: o erro precisa voltar como resposta.
        Assert.False(analise.IsValid);
        Assert.NotEmpty(analise.Errors);
    }

    [Fact]
    public void Erro_de_sintaxe_informa_a_linha()
    {
        var analise = _renderer.Analyze("<p>ok</p>\n<p>ok</p>\n{{ if vip }}");

        var erro = Assert.Single(analise.Errors);
        Assert.NotNull(erro.Line);
    }

    [Fact]
    public void Renderizar_conteudo_invalido_devolve_erro_sem_html()
    {
        var resultado = _renderer.Render("{{ if vip }}sem fechar", Valores());

        Assert.False(resultado.Succeeded);
        Assert.Null(resultado.Html);
        Assert.NotEmpty(resultado.Errors);
    }

    [Fact]
    public void Laco_excessivo_e_interrompido_em_vez_de_travar()
    {
        // Sem teto, um modelo assim seguraria a requisição indefinidamente.
        var resultado = _renderer.Render(
            "{{ for i in 1..100000 }}x{{ end }}",
            Valores());

        Assert.False(resultado.Succeeded);
        Assert.NotEmpty(resultado.Errors);
    }

    [Fact]
    public void Modelo_nao_alcanca_o_sistema_de_arquivos()
    {
        // O motor roda em sandbox: um modelo é dado, não código. Foi por isso
        // que Razor ficou de fora — lá isto seria C# executado no servidor.
        var resultado = _renderer.Render("{{ include 'C:/Windows/win.ini' }}", Valores());

        Assert.False(resultado.Succeeded);
    }

    [Fact]
    public void Valor_com_html_e_inserido_como_veio()
    {
        var resultado = _renderer.Render(
            "<p>{{ nome }}</p>",
            Valores(("nome", "<b>Pedro</b>")));

        // Comportamento consciente: modelos de e-mail costumam precisar de
        // trechos em HTML vindos de fora. Quem dispara é usuário autenticado
        // do sistema, não visitante anônimo. Se um dia entrar conteúdo de
        // origem não confiável, é aqui que a fuga de HTML precisa acontecer.
        Assert.Equal("<p><b>Pedro</b></p>", resultado.Html);
    }

    [Fact]
    public void Acentuacao_e_preservada()
    {
        var resultado = _renderer.Render(
            "<p>Olá {{ nome }}, sua inscrição está confirmada.</p>",
            Valores(("nome", "João")));

        Assert.Equal("<p>Olá João, sua inscrição está confirmada.</p>", resultado.Html);
    }
}

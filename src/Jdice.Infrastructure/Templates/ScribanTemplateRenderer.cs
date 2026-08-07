using Jdice.Application.Abstractions;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Jdice.Infrastructure.Templates;

/// <summary>
/// Renderiza os modelos com Scriban, na sintaxe <c>{{ variavel }}</c>.
/// <para>
/// Sandbox por padrão: o motor não expõe o sistema de arquivos nem tipos do
/// .NET, então um modelo é dado, não código. Foi por isso que Razor ficou de
/// fora — lá o conteúdo seria compilado e executado no servidor.
/// </para>
/// </summary>
public sealed class ScribanTemplateRenderer : ITemplateRenderer
{
    /// <summary>
    /// Tetos para conter modelo mal escrito. Um laço de milhões de iterações
    /// ou recursão profunda seguraria a requisição; o motor aborta e o erro
    /// volta como problema do conteúdo, não como falha do servidor.
    /// </summary>
    private const int LimiteDeIteracoes = 10_000;

    private const int LimiteDeRecursao = 100;

    public TemplateAnalysis Analyze(string html)
    {
        var template = Template.Parse(html ?? string.Empty);

        if (template.HasErrors)
        {
            return new TemplateAnalysis([], TraduzirErros(template.Messages));
        }

        // Percorre a árvore em vez de usar expressão regular: assim as
        // variáveis usadas dentro de condicionais e laços também aparecem, e
        // um "{{" escrito no meio de um texto não vira falso positivo.
        var variaveis = new List<string>();
        Coletar(template.Page, variaveis, dentroDeLaco: null);

        return new TemplateAnalysis(variaveis, []);
    }

    public TemplateRenderResult Render(string html, IReadOnlyDictionary<string, string?> values)
    {
        var template = Template.Parse(html ?? string.Empty);

        if (template.HasErrors)
        {
            return new TemplateRenderResult(null, TraduzirErros(template.Messages));
        }

        var script = new ScriptObject();

        foreach (var (nome, valor) in values)
        {
            // Sem valor vira string vazia: um "null" impresso no corpo do
            // e-mail seria pior do que um espaço em branco.
            script.Add(nome, valor ?? string.Empty);
        }

        var context = new TemplateContext
        {
            // Variável não declarada vira vazio em vez de estourar: quem
            // escreve o modelo não deve receber erro por citar algo que ainda
            // não preencheu no preview.
            EnableRelaxedMemberAccess = true,
            LoopLimit = LimiteDeIteracoes,
            RecursiveLimit = LimiteDeRecursao
        };

        context.PushGlobal(script);

        try
        {
            return new TemplateRenderResult(template.Render(context), []);
        }
        catch (ScriptRuntimeException exception)
        {
            return new TemplateRenderResult(
                null,
                [new TemplateError(exception.OriginalMessage, exception.Span.Start.Line + 1)]);
        }
        catch (Exception exception)
        {
            // Conteúdo enviado por quem usa o sistema não pode derrubar a
            // requisição, por mais estranho que seja.
            return new TemplateRenderResult(null, [new TemplateError(exception.Message, null)]);
        }
    }

    private static void Coletar(ScriptNode? node, List<string> destino, string? dentroDeLaco)
    {
        switch (node)
        {
            case null:
                return;

            // Variável simples: {{ nome }}
            case ScriptVariableGlobal variavel
                when variavel.Name != dentroDeLaco && !destino.Contains(variavel.Name):
                destino.Add(variavel.Name);
                return;

            case ScriptVariableGlobal:
                return;

            // Dentro de "{{ for item in lista }}", "item" é da própria iteração
            // e não é uma variável que quem envia precisa preencher.
            case ScriptForStatement laco:
                Coletar(laco.Iterator, destino, dentroDeLaco);

                var nomeDoItem = (laco.Variable as ScriptVariable)?.Name ?? dentroDeLaco;
                Coletar(laco.Body, destino, nomeDoItem);
                return;
        }

        foreach (var filho in node.Children)
        {
            Coletar(filho, destino, dentroDeLaco);
        }
    }

    private static IReadOnlyList<TemplateError> TraduzirErros(LogMessageBag mensagens) =>
        [.. mensagens
            .Where(mensagem => mensagem.Type == ParserMessageType.Error)
            .Select(mensagem => new TemplateError(mensagem.Message, mensagem.Span.Start.Line + 1))];
}

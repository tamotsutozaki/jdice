using System.Text;
using Jdice.Application.Abstractions;
using Jdice.Domain.Recipients;

namespace Jdice.Infrastructure.Recipients;

/// <summary>
/// Lê planilhas de destinatários. Escrito à mão em vez de usar biblioteca
/// porque o que importa aqui não é só separar campos — é dizer, para cada
/// linha recusada, qual era o número dela e o motivo.
/// <para>
/// Trata o que aparece em arquivo real: separador ponto e vírgula ou vírgula,
/// acentuação salva pelo Excel em Windows-1252, campos entre aspas com
/// separador dentro, e aspas duplicadas para escapar aspas.
/// </para>
/// </summary>
public sealed class CsvRecipientReader : ICsvRecipientReader
{
    private const int MaximoDeLinhas = 50_000;

    private static readonly string[] NomesDeColunaDeEmail = ["email", "e-mail", "mail"];
    private static readonly string[] NomesDeColunaDeNome = ["nome", "name"];

    static CsvRecipientReader()
    {
        // Windows-1252 não vem registrado no .NET moderno, e é justamente a
        // codificação que o Excel em português usa ao salvar como CSV.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public CsvReadResult Read(Stream conteudo)
    {
        var texto = LerComCodificacaoProvavel(conteudo);

        if (string.IsNullOrWhiteSpace(texto))
        {
            return CsvReadResult.Falha("O arquivo está vazio.");
        }

        var linhas = SepararLinhas(texto);

        if (linhas.Count == 0)
        {
            return CsvReadResult.Falha("O arquivo está vazio.");
        }

        var separador = DetectarSeparador(linhas[0]);
        var cabecalho = SepararCampos(linhas[0], separador);

        var indiceDoEmail = EncontrarColuna(cabecalho, NomesDeColunaDeEmail);

        if (indiceDoEmail < 0)
        {
            return CsvReadResult.Falha(
                "O arquivo precisa de uma coluna chamada 'email' na primeira linha.");
        }

        var indiceDoNome = EncontrarColuna(cabecalho, NomesDeColunaDeNome);

        var colunasLivres = cabecalho
            .Select((nome, indice) => (Nome: nome.Trim(), Indice: indice))
            .Where(coluna =>
                coluna.Indice != indiceDoEmail
                && coluna.Indice != indiceDoNome
                && coluna.Nome.Length > 0)
            .ToList();

        var aceitas = new List<CsvRecipientRow>();
        var erros = new List<CsvRowError>();
        var emailsVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < linhas.Count; i++)
        {
            // +1 porque a planilha conta a partir de 1 e o cabeçalho é a linha 1.
            var numeroDaLinha = i + 1;

            if (aceitas.Count >= MaximoDeLinhas)
            {
                erros.Add(new CsvRowError(
                    numeroDaLinha,
                    $"Limite de {MaximoDeLinhas:N0} linhas por arquivo atingido; o resto foi ignorado."));
                break;
            }

            var bruto = linhas[i];

            if (string.IsNullOrWhiteSpace(bruto))
            {
                continue;
            }

            var campos = SepararCampos(bruto, separador);
            var email = Recipient.NormalizeEmail(ValorEm(campos, indiceDoEmail));

            if (email.Length == 0)
            {
                erros.Add(new CsvRowError(numeroDaLinha, "E-mail em branco."));
                continue;
            }

            if (!Recipient.IsValidEmail(email))
            {
                erros.Add(new CsvRowError(numeroDaLinha, $"E-mail inválido: '{email}'."));
                continue;
            }

            if (!emailsVistos.Add(email))
            {
                // Duplicata dentro do próprio arquivo: avisar é melhor que
                // importar duas vezes em silêncio.
                erros.Add(new CsvRowError(numeroDaLinha, $"E-mail repetido no arquivo: '{email}'."));
                continue;
            }

            var livres = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var coluna in colunasLivres)
            {
                var valor = ValorEm(campos, coluna.Indice).Trim();

                if (valor.Length > 0)
                {
                    livres[coluna.Nome] = valor;
                }
            }

            aceitas.Add(new CsvRecipientRow(
                numeroDaLinha,
                email,
                indiceDoNome >= 0 ? ValorEm(campos, indiceDoNome).Trim() : string.Empty,
                livres));
        }

        return new CsvReadResult(aceitas, erros, [.. colunasLivres.Select(coluna => coluna.Nome)]);
    }

    /// <summary>
    /// Tenta UTF-8 estrito; se o conteúdo não for UTF-8 válido, relê como
    /// Windows-1252. Sem isso, um arquivo salvo pelo Excel apareceria com
    /// "JoÃ£o" no lugar de "João" — ou pior, com caracteres substituídos.
    /// </summary>
    private static string LerComCodificacaoProvavel(Stream conteudo)
    {
        using var memoria = new MemoryStream();
        conteudo.CopyTo(memoria);
        var bytes = memoria.ToArray();

        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            var utf8Estrito = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true);

            var texto = utf8Estrito.GetString(bytes);

            // Remove o marcador de ordem de bytes que o Excel costuma escrever;
            // deixado no lugar, ele grudaria na primeira coluna do cabeçalho.
            return texto.TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(1252).GetString(bytes);
        }
    }

    /// <summary>
    /// Separa as linhas respeitando quebras dentro de campos entre aspas — um
    /// endereço em duas linhas dentro de um campo não pode virar duas linhas
    /// da planilha.
    /// </summary>
    private static List<string> SepararLinhas(string texto)
    {
        var linhas = new List<string>();
        var atual = new StringBuilder();
        var dentroDeAspas = false;

        for (var i = 0; i < texto.Length; i++)
        {
            var caractere = texto[i];

            if (caractere == '"')
            {
                dentroDeAspas = !dentroDeAspas;
                atual.Append(caractere);
                continue;
            }

            if (!dentroDeAspas && (caractere == '\n' || caractere == '\r'))
            {
                // \r\n conta como uma quebra só.
                if (caractere == '\r' && i + 1 < texto.Length && texto[i + 1] == '\n')
                {
                    i++;
                }

                linhas.Add(atual.ToString());
                atual.Clear();
                continue;
            }

            atual.Append(caractere);
        }

        if (atual.Length > 0)
        {
            linhas.Add(atual.ToString());
        }

        return linhas;
    }

    /// <summary>
    /// Escolhe o separador pelo que aparece mais no cabeçalho. Planilha
    /// brasileira costuma usar ponto e vírgula, porque a vírgula é decimal.
    /// </summary>
    private static char DetectarSeparador(string cabecalho)
    {
        var candidatos = new[] { ';', ',', '\t', '|' };

        var melhor = candidatos
            .Select(separador => (Separador: separador, Contagem: ContarFora(cabecalho, separador)))
            .OrderByDescending(item => item.Contagem)
            .First();

        return melhor.Contagem > 0 ? melhor.Separador : ';';
    }

    private static int ContarFora(string linha, char separador)
    {
        var contagem = 0;
        var dentroDeAspas = false;

        foreach (var caractere in linha)
        {
            if (caractere == '"')
            {
                dentroDeAspas = !dentroDeAspas;
            }
            else if (caractere == separador && !dentroDeAspas)
            {
                contagem++;
            }
        }

        return contagem;
    }

    private static List<string> SepararCampos(string linha, char separador)
    {
        var campos = new List<string>();
        var atual = new StringBuilder();
        var dentroDeAspas = false;

        for (var i = 0; i < linha.Length; i++)
        {
            var caractere = linha[i];

            if (caractere == '"')
            {
                // Duas aspas seguidas dentro de um campo representam uma aspa.
                if (dentroDeAspas && i + 1 < linha.Length && linha[i + 1] == '"')
                {
                    atual.Append('"');
                    i++;
                    continue;
                }

                dentroDeAspas = !dentroDeAspas;
                continue;
            }

            if (caractere == separador && !dentroDeAspas)
            {
                campos.Add(atual.ToString());
                atual.Clear();
                continue;
            }

            atual.Append(caractere);
        }

        campos.Add(atual.ToString());

        return campos;
    }

    private static int EncontrarColuna(List<string> cabecalho, string[] nomesAceitos)
    {
        for (var i = 0; i < cabecalho.Count; i++)
        {
            var nome = cabecalho[i].Trim().Trim('﻿');

            if (nomesAceitos.Contains(nome, StringComparer.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Linha com menos colunas que o cabeçalho devolve vazio, não erro.</summary>
    private static string ValorEm(List<string> campos, int indice) =>
        indice >= 0 && indice < campos.Count ? campos[indice] : string.Empty;
}

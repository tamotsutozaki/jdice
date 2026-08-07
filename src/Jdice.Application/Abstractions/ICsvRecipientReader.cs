namespace Jdice.Application.Abstractions;

/// <param name="Numero">Linha no arquivo, contando o cabeçalho — é o número que a pessoa vê na planilha.</param>
public sealed record CsvRecipientRow(
    int Numero,
    string Email,
    string Nome,
    IReadOnlyDictionary<string, string> Campos);

public sealed record CsvRowError(int Linha, string Motivo);

public sealed record CsvReadResult(
    IReadOnlyList<CsvRecipientRow> Linhas,
    IReadOnlyList<CsvRowError> Erros,
    IReadOnlyList<string> ColunasLivres)
{
    public static CsvReadResult Falha(string motivo) =>
        new([], [new CsvRowError(0, motivo)], []);
}

public interface ICsvRecipientReader
{
    /// <summary>
    /// Lê a planilha e separa o que dá para importar do que não dá. Nunca
    /// lança por conteúdo malformado: linha ruim vira erro com o número dela,
    /// para que a pessoa consiga achar o problema no arquivo.
    /// </summary>
    CsvReadResult Read(Stream conteudo);
}

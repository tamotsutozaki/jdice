namespace Jdice.Application.Recipients;

/// <param name="Linha">Número da linha na planilha, como a pessoa vê no editor.</param>
public sealed record ImportIssue(int Linha, string Motivo);

/// <summary>
/// O que aconteceu com cada linha do arquivo. Existe para que quem importou
/// consiga achar o problema — recusar mil linhas por causa de três erros
/// deixaria a pessoa procurando no escuro.
/// </summary>
public sealed record ImportResult(
    int TotalDeLinhas,
    int Criados,
    int Atualizados,
    int JaNaLista,
    IReadOnlyList<ImportIssue> Recusados,
    IReadOnlyList<string> ColunasLivres)
{
    public int Importados => Criados + Atualizados;
}

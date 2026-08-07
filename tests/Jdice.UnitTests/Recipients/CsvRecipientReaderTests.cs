using System.Text;
using Jdice.Infrastructure.Recipients;

namespace Jdice.UnitTests.Recipients;

/// <summary>
/// O CSV é a única superfície do sistema que recebe arquivo de fora, com
/// formato e codificação que ninguém controla. É onde mais vale cobrir os
/// casos chatos: acentuação do Excel, separador brasileiro, aspas, linha
/// torta e duplicata.
/// </summary>
public class CsvRecipientReaderTests
{
    private readonly CsvRecipientReader _reader = new();

    private static Stream ComoUtf8(string conteudo) =>
        new MemoryStream(new UTF8Encoding(false).GetBytes(conteudo));

    [Fact]
    public void Le_planilha_simples_com_ponto_e_virgula()
    {
        var resultado = _reader.Read(ComoUtf8("""
            email;nome
            ana@empresa.com;Ana
            bruno@empresa.com;Bruno
            """));

        Assert.Empty(resultado.Erros);
        Assert.Equal(2, resultado.Linhas.Count);
        Assert.Equal("ana@empresa.com", resultado.Linhas[0].Email);
        Assert.Equal("Ana", resultado.Linhas[0].Nome);
    }

    [Fact]
    public void Le_planilha_com_virgula_como_separador()
    {
        var resultado = _reader.Read(ComoUtf8("""
            email,nome
            ana@empresa.com,Ana
            """));

        Assert.Empty(resultado.Erros);
        Assert.Equal("Ana", Assert.Single(resultado.Linhas).Nome);
    }

    [Fact]
    public void Colunas_extras_viram_campos_livres()
    {
        var resultado = _reader.Read(ComoUtf8("""
            email;nome;empresa;plano
            ana@empresa.com;Ana;Acme;Premium
            """));

        var linha = Assert.Single(resultado.Linhas);

        // São esses campos que permitem personalizar o e-mail além do nome.
        Assert.Equal("Acme", linha.Campos["empresa"]);
        Assert.Equal("Premium", linha.Campos["plano"]);
        Assert.Equal(["empresa", "plano"], resultado.ColunasLivres);
    }

    [Fact]
    public void Acentuacao_salva_pelo_excel_e_lida_corretamente()
    {
        // Excel em português salva CSV em Windows-1252, não em UTF-8.
        var bytes = Encoding.GetEncoding(1252).GetBytes("email;nome\njoao@empresa.com;João Conceição");

        var resultado = _reader.Read(new MemoryStream(bytes));

        Assert.Equal("João Conceição", Assert.Single(resultado.Linhas).Nome);
    }

    [Fact]
    public void Acentuacao_em_utf8_tambem_e_lida_corretamente()
    {
        var resultado = _reader.Read(ComoUtf8("email;nome\njoao@empresa.com;João Conceição"));

        Assert.Equal("João Conceição", Assert.Single(resultado.Linhas).Nome);
    }

    [Fact]
    public void Marcador_de_ordem_de_bytes_nao_gruda_no_cabecalho()
    {
        // O Excel escreve BOM; sem removê-lo, a coluna viraria "﻿email"
        // e o arquivo seria recusado por "não ter coluna email".
        var bytes = new UTF8Encoding(true).GetBytes("email;nome\nana@empresa.com;Ana");

        var resultado = _reader.Read(new MemoryStream(bytes));

        Assert.Empty(resultado.Erros);
        Assert.Single(resultado.Linhas);
    }

    [Fact]
    public void Campo_entre_aspas_pode_conter_o_separador()
    {
        var resultado = _reader.Read(ComoUtf8("""
            email;nome;empresa
            ana@empresa.com;Ana;"Acme, Indústria e Comércio"
            """));

        Assert.Equal("Acme, Indústria e Comércio", Assert.Single(resultado.Linhas).Campos["empresa"]);
    }

    [Fact]
    public void Aspas_duplicadas_viram_uma_aspa()
    {
        var resultado = _reader.Read(ComoUtf8("""
            email;nome
            ana@empresa.com;"Ana ""Aninha"" Souza"
            """));

        Assert.Equal("Ana \"Aninha\" Souza", Assert.Single(resultado.Linhas).Nome);
    }

    [Fact]
    public void Quebra_de_linha_dentro_de_aspas_nao_divide_a_linha()
    {
        var resultado = _reader.Read(ComoUtf8(
            "email;nome;endereco\nana@empresa.com;Ana;\"Rua A, 100\nSala 2\""));

        var linha = Assert.Single(resultado.Linhas);
        Assert.Contains("Sala 2", linha.Campos["endereco"], StringComparison.Ordinal);
    }

    [Fact]
    public void Email_invalido_vira_erro_com_o_numero_da_linha()
    {
        var resultado = _reader.Read(ComoUtf8("""
            email;nome
            ana@empresa.com;Ana
            sem-arroba;Bruno
            carla@empresa.com;Carla
            """));

        // As linhas boas entram; só a ruim é recusada.
        Assert.Equal(2, resultado.Linhas.Count);

        var erro = Assert.Single(resultado.Erros);
        Assert.Equal(3, erro.Linha);
        Assert.Contains("sem-arroba", erro.Motivo, StringComparison.Ordinal);
    }

    [Fact]
    public void Email_em_branco_vira_erro()
    {
        var resultado = _reader.Read(ComoUtf8("""
            email;nome
            ;Bruno
            """));

        Assert.Empty(resultado.Linhas);
        Assert.Equal(2, Assert.Single(resultado.Erros).Linha);
    }

    [Fact]
    public void Email_repetido_no_arquivo_e_recusado_uma_vez_so()
    {
        var resultado = _reader.Read(ComoUtf8("""
            email;nome
            ana@empresa.com;Ana
            ANA@EMPRESA.COM;Ana de novo
            """));

        // Mesma pessoa em caixa diferente continua sendo a mesma pessoa.
        Assert.Single(resultado.Linhas);
        Assert.Contains("repetido", Assert.Single(resultado.Erros).Motivo, StringComparison.Ordinal);
    }

    [Fact]
    public void Linha_com_menos_colunas_que_o_cabecalho_nao_quebra()
    {
        var resultado = _reader.Read(ComoUtf8("""
            email;nome;empresa
            ana@empresa.com;Ana
            """));

        var linha = Assert.Single(resultado.Linhas);
        Assert.Equal("Ana", linha.Nome);
        Assert.Empty(linha.Campos);
    }

    [Fact]
    public void Linhas_vazias_sao_ignoradas_sem_virar_erro()
    {
        var resultado = _reader.Read(ComoUtf8("email;nome\nana@empresa.com;Ana\n\n\n"));

        Assert.Single(resultado.Linhas);
        Assert.Empty(resultado.Erros);
    }

    [Fact]
    public void Planilha_sem_coluna_de_email_e_recusada_com_explicacao()
    {
        var resultado = _reader.Read(ComoUtf8("""
            nome;empresa
            Ana;Acme
            """));

        Assert.Empty(resultado.Linhas);
        Assert.Contains("email", Assert.Single(resultado.Erros).Motivo, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("e-mail")]
    [InlineData("E-Mail")]
    [InlineData("EMAIL")]
    [InlineData("Mail")]
    public void Cabecalho_de_email_aceita_variacoes_comuns(string nomeDaColuna)
    {
        var resultado = _reader.Read(ComoUtf8($"{nomeDaColuna};nome\nana@empresa.com;Ana"));

        Assert.Single(resultado.Linhas);
    }

    [Fact]
    public void Planilha_sem_coluna_de_nome_ainda_importa()
    {
        var resultado = _reader.Read(ComoUtf8("""
            email
            ana@empresa.com
            """));

        // O e-mail é o que importa; o nome é desejável, não obrigatório.
        var linha = Assert.Single(resultado.Linhas);
        Assert.Equal("ana@empresa.com", linha.Email);
        Assert.Equal(string.Empty, linha.Nome);
    }

    [Fact]
    public void Arquivo_vazio_e_recusado_com_explicacao()
    {
        var resultado = _reader.Read(new MemoryStream([]));

        Assert.Contains("vazio", Assert.Single(resultado.Erros).Motivo, StringComparison.Ordinal);
    }

    [Fact]
    public void Espacos_em_volta_do_email_sao_removidos()
    {
        var resultado = _reader.Read(ComoUtf8("""
            email;nome
              ana@empresa.com  ;Ana
            """));

        Assert.Equal("ana@empresa.com", Assert.Single(resultado.Linhas).Email);
    }

    [Fact]
    public void Campo_livre_vazio_nao_vira_variavel()
    {
        var resultado = _reader.Read(ComoUtf8("""
            email;nome;empresa
            ana@empresa.com;Ana;
            """));

        // Guardar "empresa" vazia faria o e-mail sair com um buraco no lugar.
        Assert.Empty(Assert.Single(resultado.Linhas).Campos);
    }

    [Fact]
    public void Terminacao_de_linha_do_windows_e_tratada()
    {
        var resultado = _reader.Read(ComoUtf8("email;nome\r\nana@empresa.com;Ana\r\n"));

        Assert.Single(resultado.Linhas);
        Assert.Equal("Ana", resultado.Linhas[0].Nome);
    }
}

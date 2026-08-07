using Jdice.Domain.Recipients;

namespace Jdice.UnitTests.Recipients;

public class RecipientTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 7, 15, 0, 0, TimeSpan.Zero);

    private static Recipient Criar(
        string email = "ana@empresa.com",
        string? nome = "Ana",
        Dictionary<string, string>? campos = null) =>
        Recipient.Create(email, nome, campos, Agora);

    [Theory]
    [InlineData("Ana@Empresa.com", "ana@empresa.com")]
    [InlineData("  ana@empresa.com  ", "ana@empresa.com")]
    public void Email_e_normalizado(string entrada, string esperado)
    {
        // Sem isso, a mesma pessoa entraria duas vezes numa importação em que
        // o arquivo trouxesse a caixa diferente.
        Assert.Equal(esperado, Criar(entrada).Email);
    }

    [Theory]
    [InlineData("ana@empresa.com")]
    [InlineData("ana.souza@empresa.com.br")]
    [InlineData("ana+promo@empresa.com")]
    [InlineData("ana_souza@sub.empresa.com")]
    [InlineData("a@b.co")]
    public void Emails_validos_sao_aceitos(string email)
    {
        Assert.True(Recipient.IsValidEmail(email));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("sem-arroba.com")]
    [InlineData("@empresa.com")]
    [InlineData("ana@")]
    [InlineData("ana@empresa")]
    [InlineData("ana@.com")]
    [InlineData("ana@empresa.")]
    [InlineData("ana souza@empresa.com")]
    [InlineData("ana@@empresa.com")]
    public void Emails_invalidos_sao_recusados(string? email)
    {
        // São exatamente os casos que aparecem numa planilha real.
        Assert.False(Recipient.IsValidEmail(email));
    }

    [Fact]
    public void Criacao_com_email_invalido_e_recusada()
    {
        var excecao = Assert.Throws<ArgumentException>(() => Criar("sem-arroba"));

        // A mensagem vira a linha do relatório de importação: precisa dizer
        // qual e-mail estava errado.
        Assert.Contains("sem-arroba", excecao.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Nome_ausente_vira_vazio_em_vez_de_impedir_o_cadastro()
    {
        // Planilha sem coluna de nome é comum; o e-mail é o que importa.
        Assert.Equal(string.Empty, Criar(nome: null).Name);
    }

    [Fact]
    public void Campos_livres_sao_guardados()
    {
        var recipient = Criar(campos: new() { ["empresa"] = "Acme", ["plano"] = "Premium" });

        Assert.Equal("Acme", recipient.Fields["empresa"]);
        Assert.Equal("Premium", recipient.Fields["plano"]);
    }

    [Fact]
    public void Campos_ignoram_diferenca_de_caixa_no_nome()
    {
        var recipient = Criar(campos: new() { ["Empresa"] = "Acme" });

        // O modelo pode escrever {{ empresa }} e a planilha trazer "Empresa".
        Assert.Equal("Acme", recipient.Fields["empresa"]);
    }

    [Fact]
    public void Campo_sem_nome_e_descartado()
    {
        var recipient = Criar(campos: new() { ["  "] = "valor solto" });

        // Coluna sem cabeçalho na planilha não vira variável.
        Assert.Empty(recipient.Fields);
    }

    [Fact]
    public void Merge_preserva_campos_que_o_arquivo_nao_trouxe()
    {
        var recipient = Criar(campos: new() { ["empresa"] = "Acme", ["plano"] = "Basico" });

        recipient.MergeFields(new Dictionary<string, string> { ["plano"] = "Premium" }, Agora);

        // Uma planilha só com "email;plano" não pode apagar a empresa.
        Assert.Equal("Acme", recipient.Fields["empresa"]);
        Assert.Equal("Premium", recipient.Fields["plano"]);
    }

    [Fact]
    public void Descadastro_vale_para_o_destinatario_inteiro()
    {
        var recipient = Criar();

        recipient.Unsubscribe(Agora);

        // Quem pede para não receber mais espera parar de receber, e não parar
        // apenas na lista de onde veio a mensagem.
        Assert.False(recipient.IsSubscribed);
        Assert.Equal(Agora, recipient.UnsubscribedAt);
    }

    [Fact]
    public void Descadastrar_duas_vezes_preserva_a_data_original()
    {
        var recipient = Criar();

        recipient.Unsubscribe(Agora);
        recipient.Unsubscribe(Agora.AddDays(1));

        Assert.Equal(Agora, recipient.UnsubscribedAt);
    }

    [Fact]
    public void Recadastro_devolve_o_envio()
    {
        var recipient = Criar();
        recipient.Unsubscribe(Agora);

        recipient.Resubscribe(Agora.AddDays(1));

        Assert.True(recipient.IsSubscribed);
        Assert.Null(recipient.UnsubscribedAt);
    }
}

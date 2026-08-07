using Jdice.Domain.Users;

namespace Jdice.UnitTests.Users;

public class PasswordPolicyTests
{
    [Fact]
    public void Senha_no_tamanho_minimo_e_aceita()
    {
        Assert.True(PasswordPolicy.IsValid(new string('a', PasswordPolicy.MinimumLength)));
    }

    [Fact]
    public void Senha_um_caractere_abaixo_do_minimo_e_recusada()
    {
        Assert.False(PasswordPolicy.IsValid(new string('a', PasswordPolicy.MinimumLength - 1)));
    }

    [Fact]
    public void Senha_no_tamanho_maximo_e_aceita()
    {
        Assert.True(PasswordPolicy.IsValid(new string('a', PasswordPolicy.MaximumLength)));
    }

    [Fact]
    public void Senha_acima_do_maximo_e_recusada()
    {
        // Limite superior existe porque o BCrypt fica mais caro conforme a
        // entrada cresce: sem teto, uma senha gigante vira custo de CPU.
        Assert.False(PasswordPolicy.IsValid(new string('a', PasswordPolicy.MaximumLength + 1)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Senha_ausente_e_recusada(string? senha)
    {
        Assert.False(PasswordPolicy.IsValid(senha));
    }

    [Fact]
    public void EnsureValid_lanca_com_o_nome_do_parametro()
    {
        var excecao = Assert.Throws<ArgumentException>(() => PasswordPolicy.EnsureValid("curta", "password"));

        Assert.Equal("password", excecao.ParamName);
    }
}

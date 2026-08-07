using Jdice.Domain.Users;

namespace Jdice.UnitTests.Users;

public class UserTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 7, 15, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Pedro@Empresa.com", "pedro@empresa.com")]
    [InlineData("  pedro@empresa.com  ", "pedro@empresa.com")]
    [InlineData("PEDRO@EMPRESA.COM", "pedro@empresa.com")]
    public void Email_e_normalizado_na_criacao(string entrada, string esperado)
    {
        // Sem isso, "Pedro@X.com" e "pedro@x.com" viram duas contas e o índice
        // único do banco não impede a duplicata.
        var user = User.Create(entrada, "hash", UserRole.User, Agora);

        Assert.Equal(esperado, user.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Criacao_sem_email_e_recusada(string? email)
    {
        Assert.Throws<ArgumentException>(() => User.Create(email!, "hash", UserRole.User, Agora));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criacao_sem_hash_de_senha_e_recusada(string hash)
    {
        Assert.Throws<ArgumentException>(
            () => User.Create("pedro@empresa.com", hash, UserRole.User, Agora));
    }

    [Fact]
    public void Usuarios_criados_no_mesmo_instante_recebem_ids_distintos()
    {
        var primeiro = User.Create("a@empresa.com", "hash", UserRole.User, Agora);
        var segundo = User.Create("b@empresa.com", "hash", UserRole.User, Agora);

        Assert.NotEqual(primeiro.Id, segundo.Id);
    }

    [Fact]
    public void Id_ordena_pela_data_de_criacao()
    {
        var antigo = User.Create("a@empresa.com", "hash", UserRole.User, Agora);
        var recente = User.Create("b@empresa.com", "hash", UserRole.User, Agora.AddMinutes(1));

        // UUIDv7 embute o timestamp nos bits mais significativos, então quem
        // foi criado depois ordena depois — é isso que mantém o índice do
        // Postgres sequencial em vez de fragmentado como seria com UUIDv4.
        Assert.True(recente.Id.CompareTo(antigo.Id) > 0);
    }

    [Fact]
    public void ChangePassword_troca_o_hash()
    {
        var user = User.Create("pedro@empresa.com", "hash-antigo", UserRole.User, Agora);

        user.ChangePassword("hash-novo");

        Assert.Equal("hash-novo", user.PasswordHash);
    }

    [Fact]
    public void ChangePassword_recusa_hash_vazio()
    {
        var user = User.Create("pedro@empresa.com", "hash-antigo", UserRole.User, Agora);

        Assert.Throws<ArgumentException>(() => user.ChangePassword("  "));
        Assert.Equal("hash-antigo", user.PasswordHash);
    }
}

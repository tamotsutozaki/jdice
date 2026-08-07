using Jdice.Infrastructure.Security;

namespace Jdice.UnitTests.Security;

public class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_nao_devolve_a_senha_em_texto_claro()
    {
        const string senha = "senha-bem-comprida-123";

        var hash = _hasher.Hash(senha);

        Assert.NotEqual(senha, hash);
        Assert.DoesNotContain(senha, hash, StringComparison.Ordinal);
    }

    [Fact]
    public void Hash_da_mesma_senha_duas_vezes_produz_resultados_diferentes()
    {
        const string senha = "senha-bem-comprida-123";

        // Salt aleatório por hash: dois usuários com a mesma senha não podem
        // ter o mesmo registro no banco.
        Assert.NotEqual(_hasher.Hash(senha), _hasher.Hash(senha));
    }

    [Fact]
    public void Verify_aceita_a_senha_correta()
    {
        const string senha = "senha-bem-comprida-123";

        Assert.True(_hasher.Verify(senha, _hasher.Hash(senha)));
    }

    [Theory]
    [InlineData("senha-errada-completamente")]
    [InlineData("senha-bem-comprida-124")]
    [InlineData("Senha-bem-comprida-123")]
    [InlineData("")]
    public void Verify_rejeita_senha_incorreta(string senhaErrada)
    {
        var hash = _hasher.Hash("senha-bem-comprida-123");

        Assert.False(_hasher.Verify(senhaErrada, hash));
    }

    [Theory]
    [InlineData("nao-e-um-hash")]
    [InlineData("")]
    [InlineData("$2a$12$formato-quase-certo-mas-invalido")]
    public void Verify_com_hash_corrompido_devolve_false_em_vez_de_explodir(string hashInvalido)
    {
        // Um registro corrompido no banco não pode virar 500 no login.
        Assert.False(_hasher.Verify("qualquer-senha", hashInvalido));
    }
}

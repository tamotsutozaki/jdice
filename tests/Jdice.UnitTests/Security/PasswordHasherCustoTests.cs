using System.Diagnostics;
using Jdice.Application.Abstractions;
using Jdice.Infrastructure.Security;

namespace Jdice.UnitTests.Security;

/// <summary>
/// O hash de referência que protege contra timing attack era calculado no
/// construtor de um serviço com escopo de requisição, o que fazia toda
/// tentativa de login pagar um BCrypt inteiro a mais. Nenhum teste funcional
/// pega isso — só um que olhe o custo.
/// </summary>
public class PasswordHasherCustoTests
{
    [Fact]
    public void Hash_de_referencia_e_calculado_uma_unica_vez()
    {
        IPasswordHasher hasher = new BcryptPasswordHasher();

        // Primeiro acesso paga a geração; os seguintes devolvem o mesmo valor
        // sem recalcular nada.
        var primeiro = hasher.ReferenceHash;

        var cronometro = Stopwatch.StartNew();

        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(primeiro, hasher.ReferenceHash);
        }

        cronometro.Stop();

        // Um BCrypt com work factor 12 leva centenas de milissegundos. 50
        // acessos que recalculassem levariam dezenas de segundos; devolver um
        // valor pronto é instantâneo. A margem é larga de propósito, para o
        // teste não ficar frágil em máquina lenta ou CI carregado.
        Assert.True(
            cronometro.ElapsedMilliseconds < 1_000,
            $"50 acessos ao hash de referência levaram {cronometro.ElapsedMilliseconds}ms — "
            + "sinal de que ele está sendo recalculado a cada acesso.");
    }

    [Fact]
    public void Hash_de_referencia_nao_corresponde_a_nenhuma_senha_previsivel()
    {
        IPasswordHasher hasher = new BcryptPasswordHasher();

        // Se alguém conseguisse adivinhar a senha por trás do hash de
        // referência, poderia entrar em qualquer conta inexistente.
        Assert.False(hasher.Verify("", hasher.ReferenceHash));
        Assert.False(hasher.Verify("senha-que-nao-existe", hasher.ReferenceHash));
        Assert.False(hasher.Verify("admin", hasher.ReferenceHash));
    }

    [Fact]
    public void Instancias_diferentes_geram_hashes_de_referencia_diferentes()
    {
        Assert.NotEqual(
            new BcryptPasswordHasher().ReferenceHash,
            new BcryptPasswordHasher().ReferenceHash);
    }
}

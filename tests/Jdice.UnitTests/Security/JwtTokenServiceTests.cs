using Jdice.Domain.Users;
using Jdice.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Jdice.UnitTests.Security;

public class JwtTokenServiceTests
{
    private const string ChaveValida = "chave-de-teste-com-mais-de-32-bytes-garantidos";

    private static readonly DateTimeOffset MomentoFixo = new(2026, 8, 7, 15, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A validação da biblioteca usa o relógio real do sistema e não aceita um
    /// TimeProvider injetado, então quem se desloca no tempo é a emissão: um
    /// token emitido com relógio no passado já nasce expirado para o validador.
    /// </summary>
    private static JwtTokenService CriarServico(DateTimeOffset momentoDaEmissao, TimeSpan? lifetime = null) =>
        new(
            Options.Create(new JwtOptions
            {
                Issuer = "jdice",
                Audience = "jdice",
                SigningKey = ChaveValida,
                Lifetime = lifetime ?? TimeSpan.FromHours(8)
            }),
            new FakeTimeProvider(momentoDaEmissao));

    private static User CriarUsuario(UserRole role = UserRole.User) =>
        User.Create("pedro@empresa.com", "hash-qualquer", role, MomentoFixo);

    private static Task<TokenValidationResult> ValidarAsync(string token, string chave = ChaveValida) =>
        new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = "jdice",
            ValidAudience = "jdice",
            IssuerSigningKey = JwtTokenService.CreateSigningKey(chave),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        });

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.User)]
    public async Task Token_carrega_a_role_no_claim(UserRole role)
    {
        var service = CriarServico(DateTimeOffset.UtcNow);

        var resultado = await ValidarAsync(service.Issue(CriarUsuario(role)).Value);

        Assert.True(resultado.IsValid);

        // Claim "role" curto, não a URI de ClaimTypes.Role: o token trafega em
        // toda requisição e não precisa carregar uma URI da Microsoft dentro.
        Assert.Equal(role.ToString(), resultado.Claims[JwtTokenService.RoleClaimType]);
    }

    [Fact]
    public async Task Token_carrega_id_e_email_do_usuario()
    {
        var service = CriarServico(DateTimeOffset.UtcNow);
        var usuario = CriarUsuario();

        var resultado = await ValidarAsync(service.Issue(usuario).Value);

        Assert.True(resultado.IsValid);
        Assert.Equal(usuario.Id.ToString(), resultado.Claims[JwtRegisteredClaimNames.Sub]);
        Assert.Equal(usuario.Email, resultado.Claims[JwtRegisteredClaimNames.Email]);
    }

    [Fact]
    public void Expiracao_e_calculada_em_UTC_a_partir_do_relogio_injetado()
    {
        var token = CriarServico(MomentoFixo, TimeSpan.FromHours(8)).Issue(CriarUsuario());

        // O projeto original somava horas no fuso da máquina e depois carimbava
        // offset -03:00 fixo, então a validade real dependia de onde o servidor
        // estivesse rodando. Aqui é sempre exatamente o lifetime configurado.
        Assert.Equal(MomentoFixo.AddHours(8), token.ExpiresAt);
        Assert.Equal(TimeSpan.Zero, token.ExpiresAt.Offset);
    }

    [Fact]
    public async Task Token_expirado_e_rejeitado()
    {
        // Emitido com o relógio a um ano atrás e validade de 1h: já venceu.
        var service = CriarServico(DateTimeOffset.UtcNow.AddYears(-1), TimeSpan.FromHours(1));

        var resultado = await ValidarAsync(service.Issue(CriarUsuario()).Value);

        Assert.False(resultado.IsValid);
        Assert.IsType<SecurityTokenExpiredException>(resultado.Exception);
    }

    [Fact]
    public async Task Token_dentro_da_validade_e_aceito()
    {
        var service = CriarServico(DateTimeOffset.UtcNow, TimeSpan.FromHours(1));

        Assert.True((await ValidarAsync(service.Issue(CriarUsuario()).Value)).IsValid);
    }

    [Fact]
    public async Task Token_assinado_com_outra_chave_e_rejeitado()
    {
        var service = CriarServico(DateTimeOffset.UtcNow);

        var resultado = await ValidarAsync(
            service.Issue(CriarUsuario()).Value,
            chave: "outra-chave-completamente-diferente-com-32-bytes");

        Assert.False(resultado.IsValid);
        Assert.IsType<SecurityTokenSignatureKeyNotFoundException>(resultado.Exception);
    }

    [Theory]
    [InlineData("curta-demais")]
    [InlineData("")]
    public void Chave_menor_que_32_bytes_e_recusada(string chaveFraca)
    {
        // HMAC-SHA256 com chave menor que o tamanho do digest enfraquece a
        // assinatura. Melhor falhar na subida do que assinar mal.
        var excecao = Assert.Throws<InvalidOperationException>(
            () => JwtTokenService.CreateSigningKey(chaveFraca));

        Assert.Contains("32 bytes", excecao.Message, StringComparison.Ordinal);
    }
}

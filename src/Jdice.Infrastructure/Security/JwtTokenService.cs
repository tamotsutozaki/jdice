using System.Security.Claims;
using System.Text;
using Jdice.Application.Abstractions;
using Jdice.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Jdice.Infrastructure.Security;

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    /// <summary>Nome do claim de papel dentro do token. Ver comentário em <see cref="Issue"/>.</summary>
    public const string RoleClaimType = "role";

    private readonly JwtOptions _options = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(User user)
    {
        // Tudo em UTC. O projeto original somava horas no fuso da máquina e
        // depois carimbava offset -03:00 fixo, então o token nascia com validade
        // errada sempre que o servidor não estava em horário de Brasília.
        var issuedAt = clock.GetUtcNow();
        var expiresAt = issuedAt.Add(_options.Lifetime);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),

                // A role viaja no token. No projeto original ela ficava só no
                // banco, obrigando uma consulta a cada request — o que anulava
                // a vantagem de ser stateless.
                //
                // Nome curto de propósito: ClaimTypes.Role expandiria para uma
                // URI da Microsoft dentro do payload, engordando um token que
                // trafega em toda requisição. Quem valida declara RoleClaimType.
                new Claim(RoleClaimType, user.Role.ToString())
            ]),
            SigningCredentials = new SigningCredentials(
                CreateSigningKey(_options.SigningKey),
                SecurityAlgorithms.HmacSha256)
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }

    /// <summary>
    /// Também usada pela API ao configurar a validação, para que assinatura e
    /// verificação nunca divirjam.
    /// </summary>
    public static SymmetricSecurityKey CreateSigningKey(string signingKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(signingKey);

        if (keyBytes.Length < JwtOptions.MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:SigningKey precisa de ao menos {JwtOptions.MinimumSigningKeyBytes} bytes " +
                $"({keyBytes.Length} informados) para assinar com HMAC-SHA256.");
        }

        return new SymmetricSecurityKey(keyBytes);
    }
}

using Jdice.Domain.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Jdice.Infrastructure.Security;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Jdice.Api.Auth;

public static class AuthorizationPolicies
{
    public const string AdminOnly = "AdminOnly";

    public static IServiceCollection AddJdiceAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Sem isso o ASP.NET reescreve os nomes dos claims na entrada:
                // "sub" vira a URI de NameIdentifier e procurar por "sub" no
                // handler devolve null. Aqui o claim chega com o nome com que
                // foi emitido.
                options.MapInboundClaims = false;

                // A validação é configurada a partir das mesmas JwtOptions usadas
                // para assinar, então emissão e verificação não podem divergir.
                options.Events = new JwtBearerEvents
                {
                    // O token não vem no header Authorization: está no cookie
                    // httpOnly, que o JavaScript não consegue ler.
                    OnMessageReceived = context =>
                    {
                        if (context.Request.Cookies.TryGetValue(AuthCookie.Name, out var token))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = JwtTokenService.CreateSigningKey(jwt.SigningKey),
                    ValidateLifetime = true,

                    // Padrão da biblioteca é 5 minutos de tolerância, o que faz
                    // um token expirado continuar valendo. Aqui expirou é expirado.
                    ClockSkew = TimeSpan.Zero,

                    // Com MapInboundClaims desligado, é preciso dizer quais
                    // claims carregam nome e papel — senão RequireRole procura
                    // pela URI longa e nunca encontra.
                    NameClaimType = JwtRegisteredClaimNames.Email,
                    RoleClaimType = JwtTokenService.RoleClaimType
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AdminOnly, policy => policy.RequireRole(nameof(UserRole.Admin)));

        return services;
    }
}

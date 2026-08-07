using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Jdice.Api.Auth;

public sealed class LoginRateLimitOptions
{
    public const string SectionName = "RateLimiting:Login";

    /// <summary>Tentativas permitidas por janela, por origem.</summary>
    public int PermitLimit { get; set; } = 10;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Desligável para os testes de integração, que fazem muitos logins seguidos.</summary>
    public bool Enabled { get; set; } = true;
}

public static class LoginRateLimiting
{
    public const string PolicyName = "login";

    /// <summary>
    /// Limita tentativas de login por IP. Sem isso o endpoint aceita força
    /// bruta à vontade — e, como o BCrypt é lento de propósito, cada tentativa
    /// custa CPU, o que transforma o próprio mecanismo de segurança em vetor de
    /// exaustão de recursos.
    /// </summary>
    public static IServiceCollection AddLoginRateLimiter(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<LoginRateLimitOptions>(configuration.GetSection(LoginRateLimitOptions.SectionName));

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.AddPolicy(PolicyName, httpContext =>
            {
                // Lido aqui, e não no registro: quando este método roda, a
                // configuração ainda não recebeu as fontes que o host acrescenta
                // depois — em teste de integração, isso fazia o limitador
                // ignorar completamente o que o teste havia configurado.
                var options = httpContext.RequestServices
                    .GetRequiredService<IOptions<LoginRateLimitOptions>>()
                    .Value;

                if (!options.Enabled)
                {
                    return RateLimitPartition.GetNoLimiter("desligado");
                }

                // Particiona por IP de origem: um atacante não deve conseguir
                // trancar o login de todo mundo estourando o limite global.
                var origem = httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecida";

                return RateLimitPartition.GetFixedWindowLimiter(origem, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.PermitLimit,
                    Window = options.Window,

                    // Sem fila: excedeu, recebe 429 na hora. Enfileirar
                    // tentativas de login só seguraria conexão à toa.
                    QueueLimit = 0
                });
            });
        });

        return services;
    }
}

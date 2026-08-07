using Jdice.Application.Abstractions;
using Jdice.Infrastructure.Persistence;
using Jdice.Infrastructure.Recipients;
using Jdice.Infrastructure.Security;
using Jdice.Infrastructure.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jdice.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<JdiceDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddScoped<IRecipientRepository, RecipientRepository>();
        services.AddScoped<IRecipientListRepository, RecipientListRepository>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddSingleton<ITemplateRenderer, ScribanTemplateRenderer>();
        services.AddSingleton<ICsvRecipientReader, CsvRecipientReader>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            // Falha na subida, não no primeiro login: um segredo ausente ou
            // fraco é erro de configuração, e erro de configuração tem que
            // aparecer no deploy.
            .ValidateOnStart();

        return services;
    }
}

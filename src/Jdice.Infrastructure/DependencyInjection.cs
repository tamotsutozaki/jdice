using Hangfire;
using Hangfire.PostgreSql;
using Jdice.Application.Abstractions;
using Jdice.Infrastructure.Email;
using Jdice.Infrastructure.Messaging;
using Jdice.Infrastructure.Persistence;
using Jdice.Infrastructure.Recipients;
using Jdice.Infrastructure.Scheduling;
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
        services.AddScoped<ICampaignRepository, CampaignRepository>();

        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddSingleton<ITemplateRenderer, ScribanTemplateRenderer>();
        services.AddSingleton<ICsvRecipientReader, CsvRecipientReader>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<ICampaignScheduler, HangfireCampaignScheduler>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            // Falha na subida, não no primeiro login: um segredo ausente ou
            // fraco é erro de configuração, e erro de configuração tem que
            // aparecer no deploy.
            .ValidateOnStart();

        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

        var filaLigada = configuration.GetValue($"{RabbitMqOptions.SectionName}:Enabled", false);

        services.AddSingleton<RabbitMqConnection>();

        // Sem a fila, o disparo roda em série dentro do job — suficiente para
        // volumes pequenos e sem depender de mais uma peça de infraestrutura.
        if (filaLigada)
        {
            services.AddSingleton<IDeliveryQueue, RabbitMqDeliveryQueue>();
        }
        else
        {
            services.AddSingleton<IDeliveryQueue, NoDeliveryQueue>();
        }

        return services;
    }

    /// <summary>
    /// Registra o Hangfire apenas como cliente: permite agendar e cancelar,
    /// mas não executa nada. É o que a API usa — quem processa é o worker.
    /// </summary>
    public static IServiceCollection AddJobScheduling(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options =>
                options.UseNpgsqlConnection(configuration.GetConnectionString("Postgres"))));

        return services;
    }
}

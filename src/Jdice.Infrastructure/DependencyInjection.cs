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
        services.AddScoped<ICampaignScheduler, HangfireCampaignScheduler>();

        AddEmailSender(services, configuration);

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            // Falha na subida, não no primeiro login: um segredo ausente ou
            // fraco é erro de configuração, e erro de configuração tem que
            // aparecer no deploy.
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
    /// Escolhe o remetente por configuração: Mailpit (SMTP de captura) em
    /// desenvolvimento, Azure Communication Services em produção. Só a seção do
    /// provedor escolhido é exigida — quem roda com Mailpit não precisa de conta
    /// na Azure, e quem roda na Azure não precisa de SMTP.
    /// </summary>
    private static void AddEmailSender(IServiceCollection services, IConfiguration configuration)
    {
        var email = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>()
            ?? new EmailOptions();

        if (email.UsaAzure)
        {
            services.AddScoped<IEmailSender, AcsEmailSender>();
            services.AddOptions<AcsOptions>()
                .Bind(configuration.GetSection(AcsOptions.SectionName))
                .ValidateDataAnnotations()
                // Provider=Azure sem connection string é erro de configuração, e
                // erro de configuração tem que quebrar na subida, não no 1º envio.
                .ValidateOnStart();
        }
        else
        {
            services.AddScoped<IEmailSender, SmtpEmailSender>();
            services.AddOptions<SmtpOptions>()
                .Bind(configuration.GetSection(SmtpOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }
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

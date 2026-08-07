using Jdice.Application.Campaigns;
using Jdice.Application.Recipients;
using Jdice.Application.Templates;
using Jdice.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Jdice.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AuthenticationService>();
        services.AddScoped<UserService>();
        services.AddScoped<TemplateService>();
        services.AddScoped<RecipientService>();
        services.AddScoped<RecipientListService>();
        services.AddScoped<CampaignService>();
        services.AddScoped<CampaignProcessor>();

        return services;
    }
}

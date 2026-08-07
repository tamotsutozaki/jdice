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

        return services;
    }
}

using Jdice.Application.Abstractions;
using Jdice.Application.Users;
using Jdice.Domain.Users;
using Jdice.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jdice.Api.Setup;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }
}

public static class DatabaseInitializer
{
    /// <summary>
    /// Aplica as migrations pendentes e, se o banco não tiver nenhuma conta,
    /// cria o admin inicial a partir da configuração.
    /// </summary>
    public static async Task InitializeAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Database");

        var context = services.GetRequiredService<JdiceDbContext>();
        await context.Database.MigrateAsync();

        var users = services.GetRequiredService<IUserRepository>();

        if (await users.AnyAsync())
        {
            return;
        }

        var seed = app.Configuration.GetSection(SeedOptions.SectionName).Get<SeedOptions>();

        if (string.IsNullOrWhiteSpace(seed?.AdminEmail) || string.IsNullOrWhiteSpace(seed.AdminPassword))
        {
            // Nunca criar um admin com credencial padrão: um "admin/admin"
            // embutido no código é a porta de entrada mais explorada que existe.
            logger.LogWarning(
                "Banco sem contas e seed não configurado. Defina {Email} e {Password} para criar o " +
                "administrador inicial — sem isso não é possível entrar no sistema.",
                $"{SeedOptions.SectionName}:{nameof(SeedOptions.AdminEmail)}",
                $"{SeedOptions.SectionName}:{nameof(SeedOptions.AdminPassword)}");

            return;
        }

        if (!PasswordPolicy.IsValid(seed.AdminPassword))
        {
            logger.LogError(
                "Senha do administrador inicial não atende à política ({Minimum} a {Maximum} caracteres). "
                + "Nenhuma conta foi criada.",
                PasswordPolicy.MinimumLength,
                PasswordPolicy.MaximumLength);

            return;
        }

        var userService = services.GetRequiredService<UserService>();
        var admin = await userService.CreateAsync(seed.AdminEmail, seed.AdminPassword, UserRole.Admin);

        logger.LogInformation("Administrador inicial criado: {Email}", admin.Email);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Jdice.Infrastructure.Persistence;

/// <summary>
/// Usada só pelo <c>dotnet ef</c>. Sem ela a ferramenta sobe o host da API
/// inteiro para achar o DbContext — o que dispararia a validação de
/// configuração e o seed a cada comando de migration.
/// </summary>
public sealed class JdiceDbContextFactory : IDesignTimeDbContextFactory<JdiceDbContext>
{
    public JdiceDbContext CreateDbContext(string[] args)
    {
        // Gerar migration não conecta no banco; a string só precisa ser válida
        // sintaticamente. Para aplicar de fato, defina JDICE_MIGRATIONS_CONNECTION.
        // Porta 5433: a 5432 costuma estar ocupada por uma instalação nativa
        // do Postgres, e cair nela dá erro de autenticação confuso.
        var connectionString =
            Environment.GetEnvironmentVariable("JDICE_MIGRATIONS_CONNECTION")
            ?? "Host=localhost;Port=5433;Database=jdice;Username=jdice;Password=jdice";

        var options = new DbContextOptionsBuilder<JdiceDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new JdiceDbContext(options);
    }
}

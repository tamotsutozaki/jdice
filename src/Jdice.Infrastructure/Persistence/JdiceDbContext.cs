using Jdice.Domain.Templates;
using Jdice.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Jdice.Infrastructure.Persistence;

public sealed class JdiceDbContext(DbContextOptions<JdiceDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Template> Templates => Set<Template>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JdiceDbContext).Assembly);
    }
}

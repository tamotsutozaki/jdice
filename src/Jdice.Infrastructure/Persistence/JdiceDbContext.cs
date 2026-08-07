using Jdice.Domain.Campaigns;
using Jdice.Domain.Recipients;
using Jdice.Domain.Templates;
using Jdice.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Jdice.Infrastructure.Persistence;

public sealed class JdiceDbContext(DbContextOptions<JdiceDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Template> Templates => Set<Template>();

    public DbSet<Recipient> Recipients => Set<Recipient>();

    public DbSet<RecipientList> RecipientLists => Set<RecipientList>();

    public DbSet<Campaign> Campaigns => Set<Campaign>();

    public DbSet<Delivery> Deliveries => Set<Delivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JdiceDbContext).Assembly);
    }
}

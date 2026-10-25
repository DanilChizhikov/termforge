using DTech.Persistence.Settings;
using DTech.Persistence.Time;
using Microsoft.EntityFrameworkCore;

namespace DTech.Persistence.Database;

public sealed class TermForgeDbContext : DbContext
{
    public TermForgeDbContext(DbContextOptions<TermForgeDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TermForgeDbContext).Assembly);
    }
}

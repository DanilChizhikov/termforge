using DTech.Core.Operations;
using DTech.Persistence.HostKeys;
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

    public DbSet<Operation> Operations => Set<Operation>();

    public DbSet<TrustedHostKeyRecord> TrustedHostKeys => Set<TrustedHostKeyRecord>();

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

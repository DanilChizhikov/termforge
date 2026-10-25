using DTech.Persistence.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DTech.Persistence.Migrations.DesignTime;

public sealed class TermForgeDbContextFactory : IDesignTimeDbContextFactory<TermForgeDbContext>
{
    public TermForgeDbContext CreateDbContext(string[] args)
    {
        var databasePath = DatabasePathResolver.ResolveDefault();
        var optionsBuilder = new DbContextOptionsBuilder<TermForgeDbContext>();
        optionsBuilder.UseTermForgeSqlite(databasePath);
        return new TermForgeDbContext(optionsBuilder.Options);
    }
}

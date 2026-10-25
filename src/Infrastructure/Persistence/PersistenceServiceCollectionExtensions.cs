using DTech.Persistence.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DTech.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(PersistenceOptions.SectionName);
        services.Configure<PersistenceOptions>(section);
        services.AddSingleton<DatabasePathResolver>();
        services.AddSingleton<DatabaseMigrator>();
        services.AddDbContext<TermForgeDbContext>(ConfigureContext);
        return services;
    }

    public static async Task MigrateDatabaseAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        var migrator = host.Services.GetRequiredService<DatabaseMigrator>();
        await migrator.MigrateAsync(cancellationToken);
    }

    private static void ConfigureContext(IServiceProvider services, DbContextOptionsBuilder options)
    {
        var pathResolver = services.GetRequiredService<DatabasePathResolver>();
        var databasePath = pathResolver.Resolve();
        options.UseTermForgeSqlite(databasePath);
    }
}

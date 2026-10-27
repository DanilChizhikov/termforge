using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DTech.Persistence.Database;

public sealed class DatabaseMigrator
{
    private readonly IServiceProvider _services;
    private readonly DatabasePathResolver _pathResolver;
    private readonly ILogger<DatabaseMigrator> _logger;

    public DatabaseMigrator(IServiceProvider services, DatabasePathResolver pathResolver, ILogger<DatabaseMigrator> logger)
    {
        _services = services;
        _pathResolver = pathResolver;
        _logger = logger;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        var databasePath = _pathResolver.Resolve();
        var databaseDirectory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        _logger.LogInformation($"Applying database migrations: {databasePath}");
        await using var scope = _services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TermForgeDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
        _logger.LogInformation("Database is up to date");
    }
}

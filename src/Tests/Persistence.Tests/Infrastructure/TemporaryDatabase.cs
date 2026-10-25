using DTech.Persistence;
using DTech.Persistence.Database;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.Persistence.Tests.Infrastructure;

internal sealed class TemporaryDatabase : IDisposable
{
    private readonly string _directory;
    private readonly ServiceProvider _services;

    public TemporaryDatabase()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"termforge-tests-{Guid.NewGuid():N}");
        DatabasePath = Path.Combine(_directory, "nested", "termforge.db");

        var settings = new Dictionary<string, string?>
        {
            [$"{PersistenceOptions.SectionName}:{nameof(PersistenceOptions.DatabasePath)}"] = DatabasePath,
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddPersistence(configuration);
        _services = collection.BuildServiceProvider();
    }

    public string DatabasePath { get; }

    public IServiceProvider Services => _services;

    public async Task MigrateAsync()
    {
        var migrator = _services.GetRequiredService<DatabaseMigrator>();
        await migrator.MigrateAsync(CancellationToken.None);
    }

    public TermForgeDbContext CreateContext()
    {
        var scope = _services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TermForgeDbContext>();
    }

    public List<Dictionary<string, object?>> Query(string sql)
    {
        var rows = new List<Dictionary<string, object?>>();
        using var connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[reader.GetName(i)] = value;
            }

            rows.Add(row);
        }

        return rows;
    }

    public void Dispose()
    {
        _services.Dispose();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}

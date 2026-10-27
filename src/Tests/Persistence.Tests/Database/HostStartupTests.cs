using DTech.Persistence.Database;
using DTech.Persistence.Settings;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DTech.Persistence.Tests.Database;

[TestClass]
public sealed class HostStartupTests
{
    [TestMethod]
    public async Task Startup_MigratesAndCreatesDatabaseInConfiguredDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"termforge-host-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(directory, "data", "termforge.db");
        try
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Configuration[$"{PersistenceOptions.SectionName}:{nameof(PersistenceOptions.DatabasePath)}"] = databasePath;
            builder.Services.AddPersistence(builder.Configuration);
            using var host = builder.Build();

            await host.MigrateDatabaseAsync();
            await host.StartAsync();

            File.Exists(databasePath).Should().BeTrue();
            using var scope = host.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TermForgeDbContext>();
            var setting = new AppSetting { Key = "first-run", Value = "done", UpdatedAt = DateTime.UtcNow };
            context.AppSettings.Add(setting);
            await context.SaveChangesAsync();
            context.AppSettings.Count().Should().Be(1);
            await host.StopAsync();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}

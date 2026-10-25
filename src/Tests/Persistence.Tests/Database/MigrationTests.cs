using DTech.Persistence.Database;
using DTech.Persistence.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DTech.Persistence.Tests.Database;

[TestClass]
public sealed class MigrationTests
{
    [TestMethod]
    public async Task Migrate_EmptyDatabase_CreatesFileAndSettingsTable()
    {
        using var database = new TemporaryDatabase();

        await database.MigrateAsync();

        File.Exists(database.DatabasePath).Should().BeTrue();
        var tables = database.Query("SELECT name FROM sqlite_master WHERE type = 'table'");
        var tableNames = tables.Select(row => (string)row["name"]!).ToList();
        tableNames.Should().Contain("app_settings");
        tableNames.Should().Contain("__EFMigrationsHistory");
    }

    [TestMethod]
    public async Task Migrate_Twice_IsIdempotent()
    {
        using var database = new TemporaryDatabase();

        await database.MigrateAsync();
        await database.MigrateAsync();

        var applied = database.Query("SELECT migration_id FROM \"__EFMigrationsHistory\"");
        using var context = database.CreateContext();
        applied.Should().HaveCount(context.Database.GetMigrations().Count());
    }

    [TestMethod]
    public async Task Migrate_AllMigrationsAreAppliedAndModelHasNoPendingChanges()
    {
        using var database = new TemporaryDatabase();
        await database.MigrateAsync();

        using var context = database.CreateContext();

        var pending = await context.Database.GetPendingMigrationsAsync();
        pending.Should().BeEmpty();
        context.Database.HasPendingModelChanges().Should().BeFalse();
    }

    [TestMethod]
    public async Task Migrate_UsesSnakeCaseNames()
    {
        using var database = new TemporaryDatabase();

        await database.MigrateAsync();

        var columns = database.Query("SELECT name FROM pragma_table_info('app_settings')");
        var columnNames = columns.Select(row => (string)row["name"]!).ToList();
        columnNames.Should().BeEquivalentTo("key", "value", "updated_at");
        var indexes = database.Query("SELECT name FROM sqlite_master WHERE type = 'index' AND tbl_name = 'app_settings'");
        indexes.Select(row => (string)row["name"]!).Should().OnlyContain(name => name == name.ToLowerInvariant());
    }

    [TestMethod]
    public void MigrationsAssembly_IsConfiguredToMigrationsProject()
    {
        using var database = new TemporaryDatabase();
        using var context = database.CreateContext();

        var assemblyName = context.GetService<IMigrationsAssembly>().Assembly.GetName().Name;

        assemblyName.Should().Be(DbContextOptionsBuilderExtensions.MigrationsAssemblyName);
    }
}

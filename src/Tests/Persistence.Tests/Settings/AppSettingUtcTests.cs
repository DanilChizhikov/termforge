using DTech.Persistence.Settings;
using DTech.Persistence.Tests.Infrastructure;
using FluentAssertions;

namespace DTech.Persistence.Tests.Settings;

[TestClass]
public sealed class AppSettingUtcTests
{
    [TestMethod]
    public async Task Save_UtcValue_IsReadBackAsEqualUtc()
    {
        using var database = new TemporaryDatabase();
        await database.MigrateAsync();
        var moment = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);

        await SaveAsync(database, "theme", moment);

        var loaded = await LoadAsync(database, "theme");
        loaded.UpdatedAt.Should().Be(moment);
        loaded.UpdatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [TestMethod]
    public async Task Save_LocalValue_IsStoredAndReadBackAsUtc()
    {
        using var database = new TemporaryDatabase();
        await database.MigrateAsync();
        var local = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Local);

        await SaveAsync(database, "theme", local);

        var loaded = await LoadAsync(database, "theme");
        loaded.UpdatedAt.Kind.Should().Be(DateTimeKind.Utc);
        loaded.UpdatedAt.Should().Be(local.ToUniversalTime());
    }

    [TestMethod]
    public async Task Save_UnspecifiedValue_IsTreatedAsUtc()
    {
        using var database = new TemporaryDatabase();
        await database.MigrateAsync();
        var unspecified = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Unspecified);

        await SaveAsync(database, "theme", unspecified);

        var loaded = await LoadAsync(database, "theme");
        loaded.UpdatedAt.Kind.Should().Be(DateTimeKind.Utc);
        loaded.UpdatedAt.Ticks.Should().Be(unspecified.Ticks);
    }

    [TestMethod]
    public async Task Save_LocalValue_PersistsUtcTextInDatabase()
    {
        using var database = new TemporaryDatabase();
        await database.MigrateAsync();
        var local = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Local);

        await SaveAsync(database, "theme", local);

        var rows = database.Query("SELECT updated_at FROM app_settings");
        var stored = DateTime.Parse((string)rows[0]["updated_at"]!, null, System.Globalization.DateTimeStyles.RoundtripKind);
        stored.Should().Be(local.ToUniversalTime());
    }

    private static async Task SaveAsync(TemporaryDatabase database, string key, DateTime updatedAt)
    {
        using var context = database.CreateContext();
        var setting = new AppSetting { Key = key, Value = "dark", UpdatedAt = updatedAt };
        context.AppSettings.Add(setting);
        await context.SaveChangesAsync();
    }

    private static async Task<AppSetting> LoadAsync(TemporaryDatabase database, string key)
    {
        using var context = database.CreateContext();
        return await context.AppSettings.FindAsync(key) ?? throw new InvalidOperationException($"Setting not found: {key}");
    }
}

using DTech.Core.AiBridge.Settings;
using DTech.Persistence.Settings;
using DTech.Persistence.Tests.Infrastructure;
using FluentAssertions;

namespace DTech.Persistence.Tests.Settings;

[TestClass]
public sealed class AiBridgeSettingsStoreTests
{
    [TestMethod]
    public async Task Load_WithoutSavedSettings_ReturnsNothing()
    {
        using var database = new TemporaryDatabase();
        await database.MigrateAsync();
        using var context = database.CreateContext();
        var store = new AiBridgeSettingsStore(context, TimeProvider.System);

        var loaded = await store.LoadAsync(CancellationToken.None);

        loaded.Should().BeNull();
    }

    [TestMethod]
    public async Task Save_ThenLoad_RoundTripsAndOverwrites()
    {
        using var database = new TemporaryDatabase();
        await database.MigrateAsync();
        var first = new AiBridgeSettings(true, true, "abc123");
        var second = first with { Enabled = false, RequireApproval = false };

        using (var context = database.CreateContext())
        {
            var store = new AiBridgeSettingsStore(context, TimeProvider.System);
            await store.SaveAsync(first, CancellationToken.None);
            await store.SaveAsync(second, CancellationToken.None);
        }

        using var reloadContext = database.CreateContext();
        var reloadStore = new AiBridgeSettingsStore(reloadContext, TimeProvider.System);
        var loaded = await reloadStore.LoadAsync(CancellationToken.None);
        loaded.Should().Be(second);
    }
}

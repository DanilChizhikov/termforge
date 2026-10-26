using DTech.Core.Terminal.Recording;
using DTech.Persistence.Settings;
using DTech.Persistence.Tests.Infrastructure;
using FluentAssertions;

namespace DTech.Persistence.Tests.Settings;

[TestClass]
public sealed class TerminalRecordingSettingsStoreTests
{
    [TestMethod]
    public async Task Load_WithoutSavedSettings_ReturnsNothing()
    {
        using var database = new TemporaryDatabase();
        await database.MigrateAsync();
        using var context = database.CreateContext();
        var store = new TerminalRecordingSettingsStore(context, TimeProvider.System);

        var loaded = await store.LoadAsync(CancellationToken.None);

        loaded.Should().BeNull();
    }

    [TestMethod]
    public async Task Save_ThenLoad_RoundTripsAndOverwrites()
    {
        using var database = new TemporaryDatabase();
        await database.MigrateAsync();
        var first = new TerminalRecordingSettings(true, 5, 20, 7);
        var second = first with { Enabled = false, MaxRecordings = 3 };

        using (var context = database.CreateContext())
        {
            var store = new TerminalRecordingSettingsStore(context, TimeProvider.System);
            await store.SaveAsync(first, CancellationToken.None);
            await store.SaveAsync(second, CancellationToken.None);
        }

        using var reloadContext = database.CreateContext();
        var reloadStore = new TerminalRecordingSettingsStore(reloadContext, TimeProvider.System);
        var loaded = await reloadStore.LoadAsync(CancellationToken.None);
        loaded.Should().Be(second);
    }
}

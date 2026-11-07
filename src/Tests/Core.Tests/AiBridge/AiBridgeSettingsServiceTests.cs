using DTech.Core.AiBridge.Settings;
using DTech.Core.Tests.AiBridge.Support;
using FluentAssertions;

namespace DTech.Core.Tests.AiBridge;

[TestClass]
public sealed class AiBridgeSettingsServiceTests
{
    private InMemoryAiBridgeSettingsStore _store = null!;
    private AiBridgeSettingsService _service = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _store = new InMemoryAiBridgeSettingsStore();
        _service = new AiBridgeSettingsService(_store);
    }

    [TestMethod]
    public async Task Get_WithoutStoredSettings_ReturnsDisabledBridgeThatRequiresApproval()
    {
        var settings = await _service.GetAsync(TestContext.CancellationToken);

        settings.Should().Be(AiBridgeSettings.Default);
        settings.Enabled.Should().BeFalse();
        settings.RequireApproval.Should().BeTrue();
    }

    [TestMethod]
    public async Task Update_PersistsPreferences()
    {
        var preferences = new AiBridgePreferences(true, false);
        var updated = await _service.UpdateAsync(preferences, TestContext.CancellationToken);

        updated.Should().Be(new AiBridgeSettings(true, false));
        _store.Stored.Should().Be(updated);
    }

    [TestMethod]
    public async Task Update_CanDisableBridgeAndRestoreApprovals()
    {
        var enabled = new AiBridgePreferences(true, false);
        await _service.UpdateAsync(enabled, TestContext.CancellationToken);
        var disabled = new AiBridgePreferences(false, true);
        await _service.UpdateAsync(disabled, TestContext.CancellationToken);

        var loaded = await _service.GetAsync(TestContext.CancellationToken);
        loaded.Should().Be(AiBridgeSettings.Default);
    }
}

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
        _service = new AiBridgeSettingsService(_store, new SequentialTokenGenerator());
    }

    [TestMethod]
    public async Task Get_WithoutStoredSettings_ReturnsDisabledBridgeThatRequiresApproval()
    {
        var settings = await _service.GetAsync(TestContext.CancellationToken);

        settings.Should().Be(AiBridgeSettings.Default);
        settings.Enabled.Should().BeFalse();
        settings.RequireApproval.Should().BeTrue();
        settings.HasToken.Should().BeFalse();
    }

    [TestMethod]
    public async Task RegenerateToken_StoresOnlyTheHash()
    {
        var token = await _service.RegenerateTokenAsync(TestContext.CancellationToken);

        token.Should().Be("token-1");
        _store.Stored!.TokenHash.Should().Be(AiBridgeTokenHasher.Hash(token));
        _store.Stored.TokenHash.Should().NotContain(token);
    }

    [TestMethod]
    public async Task RegenerateToken_InvalidatesThePreviousToken()
    {
        await _service.UpdateAsync(new AiBridgePreferences(true, true), TestContext.CancellationToken);
        var first = await _service.RegenerateTokenAsync(TestContext.CancellationToken);
        var second = await _service.RegenerateTokenAsync(TestContext.CancellationToken);

        (await _service.AcceptsTokenAsync(first, TestContext.CancellationToken)).Should().BeFalse();
        (await _service.AcceptsTokenAsync(second, TestContext.CancellationToken)).Should().BeTrue();
    }

    [TestMethod]
    public async Task AcceptsToken_WhenBridgeDisabled_ReturnsFalse()
    {
        var token = await _service.RegenerateTokenAsync(TestContext.CancellationToken);

        var accepted = await _service.AcceptsTokenAsync(token, TestContext.CancellationToken);

        accepted.Should().BeFalse();
    }

    [TestMethod]
    public async Task Update_KeepsTheTokenHash()
    {
        await _service.RegenerateTokenAsync(TestContext.CancellationToken);
        var hash = _store.Stored!.TokenHash;

        var updated = await _service.UpdateAsync(new AiBridgePreferences(true, false), TestContext.CancellationToken);

        updated.Should().Be(new AiBridgeSettings(true, false, hash));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("wrong")]
    public async Task AcceptsToken_WithWrongToken_ReturnsFalse(string? candidate)
    {
        await _service.UpdateAsync(new AiBridgePreferences(true, true), TestContext.CancellationToken);
        await _service.RegenerateTokenAsync(TestContext.CancellationToken);

        var accepted = await _service.AcceptsTokenAsync(candidate, TestContext.CancellationToken);

        accepted.Should().BeFalse();
    }
}

using DTech.Core.AiBridge.Settings;

namespace DTech.Core.Tests.AiBridge.Support;

internal sealed class InMemoryAiBridgeSettingsStore : IAiBridgeSettingsStore
{
    public AiBridgeSettings? Stored { get; set; }

    public Task<AiBridgeSettings?> LoadAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Stored);
    }

    public Task SaveAsync(AiBridgeSettings settings, CancellationToken cancellationToken)
    {
        Stored = settings;
        return Task.CompletedTask;
    }
}

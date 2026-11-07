namespace DTech.Core.AiBridge.Settings;

public sealed class AiBridgeSettingsService(IAiBridgeSettingsStore store)
{
    public async Task<AiBridgeSettings> GetAsync(CancellationToken cancellationToken)
    {
        var stored = await store.LoadAsync(cancellationToken);
        return stored ?? AiBridgeSettings.Default;
    }

    public async Task<AiBridgeSettings> UpdateAsync(AiBridgePreferences preferences, CancellationToken cancellationToken)
    {
        var current = await GetAsync(cancellationToken);
        var updated = current with { Enabled = preferences.Enabled, RequireApproval = preferences.RequireApproval };
        await store.SaveAsync(updated, cancellationToken);
        return updated;
    }
}

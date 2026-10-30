namespace DTech.Core.AiBridge.Settings;

public sealed class AiBridgeSettingsService(IAiBridgeSettingsStore store, IAiBridgeTokenGenerator tokenGenerator)
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

    public async Task<string> RegenerateTokenAsync(CancellationToken cancellationToken)
    {
        var current = await GetAsync(cancellationToken);
        var token = tokenGenerator.Create();
        var tokenHash = AiBridgeTokenHasher.Hash(token);
        var updated = current with { TokenHash = tokenHash };
        await store.SaveAsync(updated, cancellationToken);
        return token;
    }

    public async Task<bool> AcceptsTokenAsync(string? token, CancellationToken cancellationToken)
    {
        var settings = await GetAsync(cancellationToken);
        return settings.Enabled && AiBridgeTokenHasher.Matches(settings.TokenHash, token);
    }
}

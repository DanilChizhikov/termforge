namespace DTech.Core.AiBridge.Settings;

public interface IAiBridgeSettingsStore
{
    Task<AiBridgeSettings?> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(AiBridgeSettings settings, CancellationToken cancellationToken);
}

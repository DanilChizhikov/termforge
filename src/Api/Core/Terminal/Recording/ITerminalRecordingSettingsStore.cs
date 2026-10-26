namespace DTech.Core.Terminal.Recording;

public interface ITerminalRecordingSettingsStore
{
    Task<TerminalRecordingSettings?> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(TerminalRecordingSettings settings, CancellationToken cancellationToken);
}

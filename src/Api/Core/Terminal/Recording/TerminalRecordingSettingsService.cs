namespace DTech.Core.Terminal.Recording;

public sealed class TerminalRecordingSettingsService(ITerminalRecordingSettingsStore store, TerminalRecordingRegistry registry, TerminalRecordingRetention retention)
{
    public async Task<TerminalRecordingSettings> GetAsync(CancellationToken cancellationToken)
    {
        var stored = await store.LoadAsync(cancellationToken);
        if (stored is null || TerminalRecordingSettingsValidator.Validate(stored) is not null)
        {
            return TerminalRecordingSettings.Default;
        }

        return stored;
    }

    public async Task<TerminalRecordingResult<TerminalRecordingSettings>> UpdateAsync(TerminalRecordingSettings settings, CancellationToken cancellationToken)
    {
        var invalid = TerminalRecordingSettingsValidator.Validate(settings);
        if (invalid is not null)
        {
            return TerminalRecordingResult<TerminalRecordingSettings>.Failure(invalid);
        }

        await store.SaveAsync(settings, cancellationToken);
        if (!settings.Enabled)
        {
            await registry.StopAllAsync(TerminalRecordingEndReason.RecordingDisabled);
        }

        await retention.PruneAsync(settings, cancellationToken);
        return TerminalRecordingResult<TerminalRecordingSettings>.Success(settings);
    }
}

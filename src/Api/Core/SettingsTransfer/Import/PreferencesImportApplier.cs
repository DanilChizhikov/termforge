using DTech.Core.CommandHistory;
using DTech.Core.Terminal.Recording;

namespace DTech.Core.SettingsTransfer.Import;

public sealed class PreferencesImportApplier(TerminalRecordingSettingsService recordingSettings, ITerminalRecordingSettingsStore recordingStore, CommandExclusionSettings exclusionSettings)
{
    public async Task<string?> ApplyAsync(PreferencesImportPlan plan, CancellationToken cancellationToken)
    {
        if (plan.Recording is not null)
        {
            var current = await recordingSettings.GetAsync(cancellationToken);
            var settings = plan.Recording with { Enabled = current.Enabled };
            var invalid = TerminalRecordingSettingsValidator.Validate(settings);
            if (invalid is not null)
            {
                return $"Terminal recording limits were not imported: {invalid.Message}";
            }

            await recordingStore.SaveAsync(settings, cancellationToken);
        }

        if (plan.ExclusionPatterns is not null)
        {
            var invalid = await exclusionSettings.SavePatternsAsync(plan.ExclusionPatterns, cancellationToken);
            if (invalid is not null)
            {
                return $"Command history filters were not imported: {invalid}";
            }
        }

        return null;
    }

    public async Task AfterCommitAsync(PreferencesImportPlan plan, CancellationToken cancellationToken)
    {
        if (plan.Recording is not null)
        {
            var settings = await recordingSettings.GetAsync(cancellationToken);
            await recordingSettings.ApplyRuntimeAsync(settings, cancellationToken);
        }
    }
}

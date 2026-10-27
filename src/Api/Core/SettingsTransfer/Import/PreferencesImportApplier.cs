using DTech.Core.CommandHistory;
using DTech.Core.Terminal.Recording;

namespace DTech.Core.SettingsTransfer.Import;

public sealed class PreferencesImportApplier(TerminalRecordingSettingsService recordingSettings, CommandExclusionSettings exclusionSettings)
{
    public async Task<string?> ApplyAsync(PreferencesImportPlan plan, CancellationToken cancellationToken)
    {
        if (plan.Recording is not null)
        {
            var current = await recordingSettings.GetAsync(cancellationToken);
            var settings = plan.Recording with { Enabled = current.Enabled };
            var updated = await recordingSettings.UpdateAsync(settings, cancellationToken);
            if (!updated.IsSuccess)
            {
                return $"Terminal recording limits were not imported: {updated.Error.Message}";
            }
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
}

using DTech.Core.CommandHistory;
using DTech.Core.SettingsTransfer.Document;
using DTech.Core.Terminal.Recording;

namespace DTech.Core.SettingsTransfer.Import;

public sealed class PreferencesImportPlanner(TerminalRecordingSettingsService recordingSettings, CommandExclusionSettings exclusionSettings)
{
    public async Task<PreferencesImportPlan> PlanAsync(ExportedPreferences preferences, ImportMode mode, CancellationToken cancellationToken)
    {
        var changes = new List<string>();
        var current = await recordingSettings.GetAsync(cancellationToken);
        var recording = current;
        var limits = preferences.TerminalRecording;
        if (limits is not null)
        {
            recording = current with
            {
                MaxRecordingSizeMegabytes = limits.MaxRecordingSizeMegabytes,
                MaxRecordings = limits.MaxRecordings,
                RetentionDays = limits.RetentionDays,
            };
            DescribeRecordingChanges(current, recording, changes);
        }

        var currentPatterns = await exclusionSettings.GetPatternsAsync(cancellationToken);
        var patternPlan = PlanPatterns(currentPatterns, preferences.CommandExclusionPatterns, mode);
        changes.AddRange(patternPlan.Changes);
        var recordingChanged = recording != current;
        return new PreferencesImportPlan(recordingChanged ? recording : null, patternPlan.Patterns, changes);
    }

    private static void DescribeRecordingChanges(TerminalRecordingSettings current, TerminalRecordingSettings imported, List<string> changes)
    {
        if (current.MaxRecordingSizeMegabytes != imported.MaxRecordingSizeMegabytes)
        {
            changes.Add($"Maximum recording size: {current.MaxRecordingSizeMegabytes} MB → {imported.MaxRecordingSizeMegabytes} MB");
        }

        if (current.MaxRecordings != imported.MaxRecordings)
        {
            changes.Add($"Recordings kept: {current.MaxRecordings} → {imported.MaxRecordings}");
        }

        if (current.RetentionDays != imported.RetentionDays)
        {
            changes.Add($"Recording retention: {current.RetentionDays} → {imported.RetentionDays} days");
        }
    }

    private static (List<string>? Patterns, List<string> Changes) PlanPatterns(IReadOnlyList<string> current, IReadOnlyList<string>? imported, ImportMode mode)
    {
        var changes = new List<string>();
        if (imported is null)
        {
            return (null, changes);
        }

        var result = mode == ImportMode.Replace ? new List<string>() : new List<string>(current);
        var known = new HashSet<string>(result, StringComparer.Ordinal);
        for (var i = 0; i < imported.Count; i++)
        {
            if (known.Add(imported[i]))
            {
                result.Add(imported[i]);
            }
        }

        if (result.Count > CommandHistoryLimits.MaxExclusionPatterns)
        {
            changes.Add($"Command history filters: only the first {CommandHistoryLimits.MaxExclusionPatterns} patterns are kept");
            result = result.Take(CommandHistoryLimits.MaxExclusionPatterns).ToList();
        }

        if (result.SequenceEqual(current, StringComparer.Ordinal))
        {
            return (null, changes);
        }

        var currentSet = new HashSet<string>(current, StringComparer.Ordinal);
        var resultSet = new HashSet<string>(result, StringComparer.Ordinal);
        var added = result.Count(pattern => !currentSet.Contains(pattern));
        var removed = current.Count(pattern => !resultSet.Contains(pattern));
        changes.Add($"Command history filters: {added} added, {removed} removed");
        return (result, changes);
    }
}

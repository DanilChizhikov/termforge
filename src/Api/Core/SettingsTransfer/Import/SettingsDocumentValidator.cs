using DTech.Core.CommandHistory;
using DTech.Core.SettingsTransfer.Document;
using DTech.Core.Terminal.Recording;
using DTech.Core.Workspace;

namespace DTech.Core.SettingsTransfer.Import;

public static class SettingsDocumentValidator
{
    public static SettingsTransferError? Validate(SettingsDocument document)
    {
        if (document.ExportId == Guid.Empty)
        {
            return SettingsTransferError.Invalid("The settings file has no export id.");
        }

        var message = ValidateConnections(document.Connections)
            ?? ValidatePreferences(document.Preferences)
            ?? ValidateWorkspace(document.Workspace)
            ?? ValidatePinnedCommands(document.PinnedCommands);
        return message is null ? null : SettingsTransferError.Invalid(message);
    }

    private static string? ValidateConnections(IReadOnlyList<ExportedConnection>? connections)
    {
        if (connections is null)
        {
            return null;
        }

        if (connections.Count > SettingsTransferLimits.MaxConnections)
        {
            return $"The settings file contains more than {SettingsTransferLimits.MaxConnections} server connections.";
        }

        var ids = new HashSet<Guid>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < connections.Count; i++)
        {
            var invalid = ImportedConnectionValidator.Validate(connections[i]);
            if (invalid is not null)
            {
                return invalid;
            }

            var connection = connections[i];
            var name = connection.Name!.Trim();
            if (!ids.Add(connection.Id) || !names.Add(name))
            {
                return $"The settings file contains server '{name}' more than once.";
            }
        }

        return ValidateJumpHosts(connections, ids);
    }

    private static string? ValidateJumpHosts(IReadOnlyList<ExportedConnection> connections, HashSet<Guid> ids)
    {
        var jumpHosts = new Dictionary<Guid, Guid?>();
        for (var i = 0; i < connections.Count; i++)
        {
            var connection = connections[i];
            if (connection.JumpHostId is { } jumpHostId && !ids.Contains(jumpHostId))
            {
                return $"The jump host of server '{connection.Name}' is not in the settings file.";
            }

            jumpHosts[connection.Id] = connection.JumpHostId;
        }

        for (var i = 0; i < connections.Count; i++)
        {
            if (HasJumpHostLoop(connections[i].Id, jumpHosts))
            {
                return $"The jump host chain of server '{connections[i].Name}' loops back to itself.";
            }
        }

        return null;
    }

    private static bool HasJumpHostLoop(Guid start, Dictionary<Guid, Guid?> jumpHosts)
    {
        var visited = new HashSet<Guid> { start };
        var current = jumpHosts[start];
        while (current is { } currentId)
        {
            if (!visited.Add(currentId))
            {
                return true;
            }

            current = jumpHosts[currentId];
        }

        return false;
    }

    private static string? ValidatePreferences(ExportedPreferences? preferences)
    {
        if (preferences is null)
        {
            return null;
        }

        var recording = preferences.TerminalRecording;
        if (recording is not null)
        {
            var settings = new TerminalRecordingSettings(false, recording.MaxRecordingSizeMegabytes, recording.MaxRecordings, recording.RetentionDays);
            var invalid = TerminalRecordingSettingsValidator.Validate(settings);
            if (invalid is not null)
            {
                return invalid.Message;
            }
        }

        return ValidatePatterns(preferences.CommandExclusionPatterns);
    }

    private static string? ValidatePatterns(IReadOnlyList<string>? patterns)
    {
        if (patterns is null)
        {
            return null;
        }

        if (patterns.Count > CommandHistoryLimits.MaxExclusionPatterns)
        {
            return $"At most {CommandHistoryLimits.MaxExclusionPatterns} command history exclusion patterns can be imported.";
        }

        for (var i = 0; i < patterns.Count; i++)
        {
            var pattern = patterns[i];
            var invalid = pattern is null ? "A command history exclusion pattern is empty." : CommandExclusionRules.Validate(pattern);
            if (invalid is not null)
            {
                return invalid;
            }
        }

        return null;
    }

    private static string? ValidateWorkspace(WorkspaceState? workspace)
    {
        if (workspace is null)
        {
            return null;
        }

        var invalid = WorkspaceStateValidator.Validate(workspace);
        return invalid is null ? null : $"The workspace layout is invalid: {invalid.Message}";
    }

    private static string? ValidatePinnedCommands(IReadOnlyList<ExportedPinnedCommand>? commands)
    {
        if (commands is null)
        {
            return null;
        }

        if (commands.Count > SettingsTransferLimits.MaxPinnedCommands)
        {
            return $"The settings file contains more than {SettingsTransferLimits.MaxPinnedCommands} pinned commands.";
        }

        for (var i = 0; i < commands.Count; i++)
        {
            var command = commands[i];
            var valid = command is not null
                && command.ConnectionId != Guid.Empty
                && !string.IsNullOrWhiteSpace(command.Command)
                && command.Command.Length <= CommandHistoryLimits.MaxCommandLength
                && !ImportedTextRules.HasControlCharacters(command.Command);
            if (!valid)
            {
                return "A pinned command is empty, too long or contains control characters.";
            }
        }

        return null;
    }
}

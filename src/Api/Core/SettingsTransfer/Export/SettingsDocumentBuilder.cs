using DTech.Core.CommandHistory;
using DTech.Core.SavedConnections;
using DTech.Core.SettingsTransfer.Document;
using DTech.Core.Terminal.Recording;
using DTech.Core.Workspace;

namespace DTech.Core.SettingsTransfer.Export;

public sealed class SettingsDocumentBuilder
{
    private readonly ISavedConnectionRepository _connections;
    private readonly TerminalRecordingSettingsService _recordingSettings;
    private readonly CommandExclusionSettings _exclusionSettings;
    private readonly WorkspaceStateLoader _workspaceLoader;
    private readonly PinnedCommandReader _pinnedCommandReader;

    public SettingsDocumentBuilder(
        ISavedConnectionRepository connections,
        TerminalRecordingSettingsService recordingSettings,
        CommandExclusionSettings exclusionSettings,
        WorkspaceStateLoader workspaceLoader,
        PinnedCommandReader pinnedCommandReader)
    {
        _connections = connections;
        _recordingSettings = recordingSettings;
        _exclusionSettings = exclusionSettings;
        _workspaceLoader = workspaceLoader;
        _pinnedCommandReader = pinnedCommandReader;
    }

    public async Task<SettingsDocument> BuildAsync(SettingsDocumentHeader header, SettingsSections sections, CancellationToken cancellationToken)
    {
        var connections = await _connections.ListAsync(cancellationToken);
        var exportedConnections = sections.Connections ? ExportConnections(connections) : null;
        var preferences = sections.Preferences ? await ExportPreferencesAsync(cancellationToken) : null;
        var workspace = sections.Workspace ? await ExportWorkspaceAsync(cancellationToken) : null;
        var pinnedCommands = sections.PinnedCommands ? await ExportPinnedCommandsAsync(connections, cancellationToken) : null;
        return new SettingsDocument
        {
            Format = SettingsDocumentSchema.Format,
            SchemaVersion = SettingsDocumentSchema.CurrentVersion,
            ExportId = header.ExportId,
            ExportedAt = header.ExportedAt,
            Connections = exportedConnections,
            Preferences = preferences,
            Workspace = workspace,
            PinnedCommands = pinnedCommands,
        };
    }

    private static List<ExportedConnection> ExportConnections(IReadOnlyList<SavedConnection> connections)
    {
        var exported = new List<ExportedConnection>(connections.Count);
        for (var i = 0; i < connections.Count; i++)
        {
            var connection = ExportedConnectionMapper.ToExported(connections[i]);
            exported.Add(connection);
        }

        return exported;
    }

    private async Task<ExportedPreferences> ExportPreferencesAsync(CancellationToken cancellationToken)
    {
        var recording = await _recordingSettings.GetAsync(cancellationToken);
        var patterns = await _exclusionSettings.GetPatternsAsync(cancellationToken);
        var limits = new ExportedRecordingLimits
        {
            MaxRecordingSizeMegabytes = recording.MaxRecordingSizeMegabytes,
            MaxRecordings = recording.MaxRecordings,
            RetentionDays = recording.RetentionDays,
        };
        return new ExportedPreferences { TerminalRecording = limits, CommandExclusionPatterns = patterns.ToList() };
    }

    private async Task<WorkspaceState> ExportWorkspaceAsync(CancellationToken cancellationToken)
    {
        var restore = await _workspaceLoader.LoadAsync(cancellationToken);
        return restore.Snapshot.State;
    }

    private async Task<List<ExportedPinnedCommand>> ExportPinnedCommandsAsync(IReadOnlyList<SavedConnection> connections, CancellationToken cancellationToken)
    {
        var pinned = await _pinnedCommandReader.ReadAsync(connections, cancellationToken);
        var exported = new List<ExportedPinnedCommand>(pinned.Count);
        for (var i = 0; i < pinned.Count; i++)
        {
            var entry = pinned[i];
            var command = new ExportedPinnedCommand { ConnectionId = entry.ConnectionId, Command = entry.Command };
            exported.Add(command);
        }

        return exported;
    }
}

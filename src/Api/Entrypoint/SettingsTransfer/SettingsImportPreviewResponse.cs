using DTech.Core.SettingsTransfer.Import;

namespace DTech.Entrypoint.SettingsTransfer;

public sealed record SettingsImportPreviewResponse
{
    public required DateTime ExportedAt { get; init; }

    public required SettingsSectionsBody SectionsInFile { get; init; }

    public required SettingsSectionsBody SectionsToApply { get; init; }

    public required IReadOnlyList<ConnectionImportStepResponse> Connections { get; init; }

    public required IReadOnlyList<RemovedConnectionResponse> Removals { get; init; }

    public required IReadOnlyList<string> PreferenceChanges { get; init; }

    public WorkspaceImportSummary? Workspace { get; init; }

    public int PinnedCommands { get; init; }

    public required SecretsPreviewResponse Secrets { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

    public static SettingsImportPreviewResponse From(SettingsImportPreview preview)
    {
        var connections = new List<ConnectionImportStepResponse>(preview.Connections.Count);
        for (var i = 0; i < preview.Connections.Count; i++)
        {
            var step = ConnectionImportStepResponse.From(preview.Connections[i], preview);
            connections.Add(step);
        }

        var removals = new List<RemovedConnectionResponse>(preview.Removals.Count);
        for (var i = 0; i < preview.Removals.Count; i++)
        {
            var removal = preview.Removals[i];
            var response = new RemovedConnectionResponse(removal.Id, removal.Details.Name, removal.Details.Host);
            removals.Add(response);
        }

        var secrets = new SecretsPreviewResponse(preview.ConnectionsWithPassword.Count, preview.ConnectionsWithSudoPassword.Count);
        return new SettingsImportPreviewResponse
        {
            ExportedAt = preview.ExportedAt,
            SectionsInFile = SettingsSectionsBody.From(preview.SectionsInFile),
            SectionsToApply = SettingsSectionsBody.From(preview.SectionsToApply),
            Connections = connections,
            Removals = removals,
            PreferenceChanges = preview.PreferenceChanges,
            Workspace = preview.Workspace,
            PinnedCommands = preview.PinnedCommands,
            Secrets = secrets,
            Warnings = preview.Warnings,
        };
    }
}

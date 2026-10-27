using DTech.Core.Workspace;

namespace DTech.Core.SettingsTransfer.Import;

public sealed class WorkspaceImportApplier(IWorkspaceStateStore store, WorkspaceStateSaver saver)
{
    public async Task<string?> ApplyAsync(WorkspaceState imported, SettingsImportPlan plan, CancellationToken cancellationToken)
    {
        var remapped = WorkspaceIdRemapper.Remap(imported, plan);
        var drafts = await store.LoadDraftsAsync(cancellationToken);
        var withDraftTabs = WorkspaceDraftTabs.IncludeDraftTabs(remapped, drafts);
        var snapshot = new WorkspaceSnapshot(withDraftTabs, drafts);
        var saved = await saver.SaveAsync(snapshot, cancellationToken);
        return saved.IsSuccess ? null : $"The workspace layout was not imported: {saved.Error.Message}";
    }
}

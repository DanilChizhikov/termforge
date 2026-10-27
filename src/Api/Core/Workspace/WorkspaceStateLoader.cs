namespace DTech.Core.Workspace;

public sealed class WorkspaceStateLoader(IWorkspaceStateStore store, WorkspaceStateReader reader, WorkspaceConnectionFilter connectionFilter)
{
    public async Task<WorkspaceRestore> LoadAsync(CancellationToken cancellationToken)
    {
        var document = await store.LoadDocumentAsync(cancellationToken);
        var drafts = await store.LoadDraftsAsync(cancellationToken);
        var state = WorkspaceState.Empty;
        WorkspaceRestoreWarning? warning = null;
        if (document is not null)
        {
            var read = reader.Read(document);
            if (read.IsSuccess)
            {
                state = read.State;
            }
            else
            {
                warning = read.Warning;
                await store.DeleteDocumentAsync(cancellationToken);
            }
        }

        var stateWithDraftTabs = WorkspaceDraftTabs.IncludeDraftTabs(state, drafts);
        var snapshot = new WorkspaceSnapshot(stateWithDraftTabs, drafts);
        var filtered = await connectionFilter.ApplyAsync(snapshot, cancellationToken);
        return new WorkspaceRestore(filtered, warning);
    }
}

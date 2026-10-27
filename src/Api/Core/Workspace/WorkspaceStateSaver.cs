namespace DTech.Core.Workspace;

public sealed class WorkspaceStateSaver(IWorkspaceStateStore store, WorkspaceConnectionFilter connectionFilter, WorkspaceStateWriteCoordinator writeCoordinator)
{
    public async Task<WorkspaceSaveResult> SaveAsync(WorkspaceSnapshot? snapshot, CancellationToken cancellationToken)
    {
        var invalid = WorkspaceSnapshotValidator.Validate(snapshot);
        if (invalid is not null)
        {
            return WorkspaceSaveResult.Rejected(invalid);
        }

        var filtered = await connectionFilter.ApplyAsync(snapshot!, cancellationToken);
        var document = WorkspaceStateSerializer.Serialize(filtered.State);
        await writeCoordinator.RunAsync(token => store.SaveAsync(document, filtered.Drafts, token), cancellationToken);
        return WorkspaceSaveResult.Saved;
    }
}

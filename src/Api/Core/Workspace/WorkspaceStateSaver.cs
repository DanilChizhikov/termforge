namespace DTech.Core.Workspace;

public sealed class WorkspaceStateSaver(IWorkspaceStateStore store, WorkspaceConnectionFilter connectionFilter, WorkspaceStateWriteCoordinator writeCoordinator)
{
    public async Task<WorkspaceSaveResult> SaveAsync(WorkspaceSnapshot? snapshot, CancellationToken cancellationToken)
    {
        WorkspaceSaveResult result = null!;
        await writeCoordinator.RunAsync(async token => result = await SaveWithinWriteAsync(snapshot, token), cancellationToken);
        return result;
    }

    public async Task<WorkspaceSaveResult> SaveWithinWriteAsync(WorkspaceSnapshot? snapshot, CancellationToken cancellationToken)
    {
        var invalid = WorkspaceSnapshotValidator.Validate(snapshot);
        if (invalid is not null)
        {
            return WorkspaceSaveResult.Rejected(invalid);
        }

        var filtered = await connectionFilter.ApplyAsync(snapshot!, cancellationToken);
        var document = WorkspaceStateSerializer.Serialize(filtered.State);
        await store.SaveAsync(document, filtered.Drafts, cancellationToken);
        return WorkspaceSaveResult.Saved;
    }
}

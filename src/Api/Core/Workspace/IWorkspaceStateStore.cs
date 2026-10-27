namespace DTech.Core.Workspace;

public interface IWorkspaceStateStore
{
    Task<StoredWorkspaceDocument?> LoadDocumentAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspaceEditorDraft>> LoadDraftsAsync(CancellationToken cancellationToken);

    Task SaveAsync(StoredWorkspaceDocument document, IReadOnlyList<WorkspaceEditorDraft> drafts, CancellationToken cancellationToken);

    Task DeleteDocumentAsync(CancellationToken cancellationToken);
}

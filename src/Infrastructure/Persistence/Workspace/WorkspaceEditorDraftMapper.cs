using DTech.Core.Editor;
using DTech.Core.Workspace;

namespace DTech.Persistence.Workspace;

internal static class WorkspaceEditorDraftMapper
{
    public static WorkspaceEditorDraftEntity ToEntity(WorkspaceEditorDraft draft, DateTime updatedAt)
    {
        return new WorkspaceEditorDraftEntity
        {
            ConnectionId = draft.ConnectionId,
            Path = draft.Path,
            Content = draft.Content,
            BaseModifiedAt = draft.BaseVersion?.ModifiedUtc,
            BaseSize = draft.BaseVersion?.Size,
            BaseSha256 = draft.BaseVersion?.Sha256,
            UpdatedAt = updatedAt,
        };
    }

    public static WorkspaceEditorDraft ToModel(WorkspaceEditorDraftEntity entity)
    {
        var baseVersion = ToBaseVersion(entity);
        return new WorkspaceEditorDraft(entity.ConnectionId, entity.Path, entity.Content, baseVersion);
    }

    private static RemoteFileVersion? ToBaseVersion(WorkspaceEditorDraftEntity entity)
    {
        if (entity.BaseModifiedAt is not { } modifiedAt || entity.BaseSize is not { } size || entity.BaseSha256 is null)
        {
            return null;
        }

        return new RemoteFileVersion(modifiedAt, size, entity.BaseSha256);
    }
}

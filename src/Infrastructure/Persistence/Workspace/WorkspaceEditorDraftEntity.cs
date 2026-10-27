namespace DTech.Persistence.Workspace;

public sealed class WorkspaceEditorDraftEntity
{
    public Guid ConnectionId { get; set; }

    public required string Path { get; set; }

    public required string Content { get; set; }

    public DateTime? BaseModifiedAt { get; set; }

    public long? BaseSize { get; set; }

    public string? BaseSha256 { get; set; }

    public DateTime UpdatedAt { get; set; }
}

namespace DTech.Persistence.Workspace;

public sealed class WorkspaceStateEntity
{
    public required string Id { get; set; }

    public int SchemaVersion { get; set; }

    public required string Document { get; set; }

    public DateTime UpdatedAt { get; set; }
}

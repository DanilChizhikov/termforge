namespace DTech.Core.AiBridge.Approvals;

public sealed record AiActionDescription(Guid ConnectionId, string ServerName, AiActionKind Kind, string Summary)
{
    public AiFileChange? FileChange { get; init; }
}

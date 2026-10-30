namespace DTech.Core.AiBridge.Approvals;

public sealed record AiApprovalTimeout(TimeSpan Value)
{
    public static AiApprovalTimeout Default { get; } = new(TimeSpan.FromMinutes(2));
}

using DTech.Core.AiBridge.Approvals;

namespace DTech.Entrypoint.AiBridge;

internal sealed record AiApprovalResponse(Guid Id, Guid ConnectionId, string ServerName, string Kind, string Summary, AiFileChangeResponse? FileChange, DateTimeOffset ExpiresAt)
{
    public static AiApprovalResponse From(PendingAiApproval approval)
    {
        var action = approval.Action;
        var fileChange = action.FileChange is null ? null : new AiFileChangeResponse(action.FileChange.Path, action.FileChange.Before, action.FileChange.After);
        var kind = KindOf(action.Kind);
        return new AiApprovalResponse(approval.Id, action.ConnectionId, action.ServerName, kind, action.Summary, fileChange, approval.ExpiresAt);
    }

    public static IReadOnlyList<AiApprovalResponse> FromAll(IReadOnlyList<PendingAiApproval> approvals)
    {
        var responses = new List<AiApprovalResponse>(approvals.Count);
        for (var i = 0; i < approvals.Count; i++)
        {
            var response = From(approvals[i]);
            responses.Add(response);
        }

        return responses;
    }

    private static string KindOf(AiActionKind kind)
    {
        return kind switch
        {
            AiActionKind.Command => "command",
            AiActionKind.TerminalInput => "terminalInput",
            AiActionKind.ContainerAction => "containerAction",
            _ => "fileWrite",
        };
    }
}

internal sealed record AiFileChangeResponse(string Path, string Before, string After);

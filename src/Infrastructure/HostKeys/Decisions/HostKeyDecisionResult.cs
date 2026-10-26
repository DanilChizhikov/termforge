namespace DTech.HostKeys.Decisions;

public sealed record HostKeyDecisionResult(HostKeyDecisionErrorCode? ErrorCode, string? ErrorMessage)
{
    public bool IsSuccess => ErrorCode is null;

    public static HostKeyDecisionResult Success()
    {
        return new HostKeyDecisionResult(null, null);
    }

    public static HostKeyDecisionResult Failure(HostKeyDecisionErrorCode errorCode, string errorMessage)
    {
        return new HostKeyDecisionResult(errorCode, errorMessage);
    }
}

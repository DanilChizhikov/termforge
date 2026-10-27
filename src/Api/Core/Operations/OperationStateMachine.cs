namespace DTech.Core.Operations;

public static class OperationStateMachine
{
    public static bool IsTerminal(OperationStatus status)
    {
        return status is OperationStatus.Succeeded or OperationStatus.Failed or OperationStatus.Cancelled;
    }

    public static bool CanTransition(OperationStatus from, OperationStatus to)
    {
        return from switch
        {
            OperationStatus.Queued => to is OperationStatus.Running or OperationStatus.Cancelled,
            OperationStatus.Running => to is OperationStatus.Succeeded or OperationStatus.Failed or OperationStatus.Cancelled,
            _ => false,
        };
    }
}

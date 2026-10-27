namespace DTech.Entrypoint.Lifecycle;

internal static class ExitStateNames
{
    public const string Running = "running";
    public const string WaitingForTransfers = "waitingForTransfers";
    public const string ShuttingDown = "shuttingDown";

    public static string Of(ApplicationExitState state)
    {
        return state switch
        {
            ApplicationExitState.WaitingForTransfers => WaitingForTransfers,
            ApplicationExitState.ShuttingDown => ShuttingDown,
            _ => Running,
        };
    }
}

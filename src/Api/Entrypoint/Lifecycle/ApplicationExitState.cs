namespace DTech.Entrypoint.Lifecycle;

internal enum ApplicationExitState
{
    Running,
    WaitingForTransfers,
    ShuttingDown,
}

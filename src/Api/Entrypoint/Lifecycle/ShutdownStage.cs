namespace DTech.Entrypoint.Lifecycle;

internal enum ShutdownStage
{
    StopAcceptingWork = 0,
    CancelTransfers = 1,
    CloseSessions = 2,
    FlushJournal = 3,
    FlushWorkspace = 4,
}

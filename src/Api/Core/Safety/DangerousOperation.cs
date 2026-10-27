namespace DTech.Core.Safety;

public enum DangerousOperation
{
    RemoteDelete,
    ContainerStop,
    ContainerRestart,
    FileRollback,
    TransferOverwrite,
}

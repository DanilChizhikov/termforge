namespace DTech.Core.Safety;

public enum DangerousOperation
{
    RemoteDelete,
    ContainerStop,
    ContainerRestart,
    ContainerRemove,
    ImageRemove,
    ComposeRestart,
    ComposeRecreate,
    FileRollback,
    TransferOverwrite,
}

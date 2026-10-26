namespace DTech.Core.Editor;

public sealed record JournaledSave(RemoteSaveResult Result, Guid OperationId)
{
    public RemoteDocument? Document { get; init; }
}

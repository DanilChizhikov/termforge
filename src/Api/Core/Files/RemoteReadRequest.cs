namespace DTech.Core.Files;

public sealed record RemoteReadRequest(string RemotePath, Stream Destination, long Offset)
{
    public IProgress<long>? Progress { get; init; }
}

namespace DTech.Core.Files;

public sealed record RemoteWriteRequest(Stream Source, string RemotePath, long Offset)
{
    public IProgress<long>? Progress { get; init; }
}

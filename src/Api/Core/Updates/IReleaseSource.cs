namespace DTech.Core.Updates;

public interface IReleaseSource
{
    Task<ReleaseLookup> GetLatestRelease(CancellationToken cancellationToken);
}

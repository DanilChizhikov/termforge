namespace DTech.Core.Files.Remote;

public interface IRemoteConnectionOpener
{
    Task<FileResult<IRemoteFileClient>> OpenAsync(Guid connectionId, CancellationToken cancellationToken);
}

using DTech.Core.Files;

namespace DTech.Core.Transfers;

public interface ITransferConnectionOpener
{
    Task<FileResult<IRemoteFileClient>> OpenAsync(Guid connectionId, CancellationToken cancellationToken);
}

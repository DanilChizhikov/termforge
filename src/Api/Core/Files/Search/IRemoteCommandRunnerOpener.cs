using DTech.Core.Diagnostics;

namespace DTech.Core.Files.Search;

public interface IRemoteCommandRunnerOpener
{
    Task<FileResult<IRemoteCommandRunner>> OpenAsync(Guid connectionId, CancellationToken cancellationToken);
}

using DTech.Core.Files.Remote;

namespace DTech.Core.Files.Search;

public sealed class RemoteFileSearchService
{
    private readonly IRemoteConnectionOpener _connectionOpener;
    private readonly RemotePathNormalizer _normalizer;
    private readonly RemoteFindLauncher _findLauncher;
    private readonly FileTreeSearcher _searcher;

    public RemoteFileSearchService(IRemoteConnectionOpener connectionOpener, RemotePathNormalizer normalizer, RemoteFindLauncher findLauncher, FileTreeSearcher searcher)
    {
        _connectionOpener = connectionOpener;
        _normalizer = normalizer;
        _findLauncher = findLauncher;
        _searcher = searcher;
    }

    public async Task<FileResult<IFileSearchExecution>> StartAsync(Guid connectionId, RemoteFileSearchRequest request, CancellationToken cancellationToken)
    {
        var query = CreateQuery(request.Search);
        if (!query.IsSuccess)
        {
            return FileResult<IFileSearchExecution>.Failure(query.Error);
        }

        string? notice = null;
        if (request.UseRemoteFind)
        {
            var launch = await _findLauncher.LaunchAsync(connectionId, query.Value, cancellationToken);
            if (launch.Execution is not null)
            {
                return FileResult<IFileSearchExecution>.Success(launch.Execution);
            }

            notice = $"Remote find is not used: {launch.FallbackReason}. Searching over SFTP instead.";
        }

        var opened = await _connectionOpener.OpenAsync(connectionId, cancellationToken);
        if (!opened.IsSuccess)
        {
            return FileResult<IFileSearchExecution>.Failure(opened.Error);
        }

        var reader = new SftpFileTreeReader(opened.Value);
        var execution = new TreeWalkSearchExecution(_searcher, reader, query.Value) { Notice = notice };
        return FileResult<IFileSearchExecution>.Success(execution);
    }

    private FileResult<FileSearchQuery> CreateQuery(FileSearchRequest request)
    {
        var criteria = FileSearchCriteriaParser.Parse(request);
        if (!criteria.IsSuccess)
        {
            return FileResult<FileSearchQuery>.Failure(criteria.Error);
        }

        var root = _normalizer.Normalize(request.RootPath);
        if (!root.IsSuccess)
        {
            return FileResult<FileSearchQuery>.Failure(root.Error);
        }

        var query = new FileSearchQuery(root.Value, criteria.Value);
        return FileResult<FileSearchQuery>.Success(query);
    }
}

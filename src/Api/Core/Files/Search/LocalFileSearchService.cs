using DTech.Core.Files.Local;

namespace DTech.Core.Files.Search;

public sealed class LocalFileSearchService(LocalPathResolver pathResolver, LocalFileTreeReader reader, FileTreeSearcher searcher)
{
    public FileResult<IFileSearchExecution> Start(FileSearchRequest request)
    {
        var criteria = FileSearchCriteriaParser.Parse(request);
        if (!criteria.IsSuccess)
        {
            return FileResult<IFileSearchExecution>.Failure(criteria.Error);
        }

        var root = pathResolver.Resolve(request.RootPath);
        if (!root.IsSuccess)
        {
            return FileResult<IFileSearchExecution>.Failure(root.Error);
        }

        var rootRejection = FindRootRejection(root.Value);
        if (rootRejection is not null)
        {
            return FileResult<IFileSearchExecution>.Failure(rootRejection);
        }

        var query = new FileSearchQuery(root.Value, criteria.Value);
        var execution = new TreeWalkSearchExecution(searcher, reader, query);
        return FileResult<IFileSearchExecution>.Success(execution);
    }

    private static FileError? FindRootRejection(string rootPath)
    {
        if (Directory.Exists(rootPath))
        {
            return null;
        }

        return File.Exists(rootPath) ? FileError.NotADirectory(rootPath) : FileError.NotFound(rootPath);
    }
}

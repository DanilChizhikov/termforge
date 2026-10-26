using DTech.Core.Files;
using DTech.Core.Files.Search;

namespace DTech.Core.Tests.Files.Search;

internal sealed class InMemoryFileTree : IFileTreeReader
{
    private readonly Dictionary<string, List<FileEntry>> _directories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FileError> _failures = new(StringComparer.Ordinal);

    public FileSearchMethod Method => FileSearchMethod.Sftp;

    public List<string> ListedPaths { get; } = [];

    public Func<string, CancellationToken, Task>? BeforeList { get; set; }

    public bool IsDisposed { get; private set; }

    public InMemoryFileTree AddDirectory(string path)
    {
        if (!_directories.ContainsKey(path))
        {
            _directories[path] = [];
        }

        var parent = GetParent(path);
        if (parent is null)
        {
            return this;
        }

        AddDirectory(parent);
        var name = GetName(path);
        var siblings = _directories[parent];
        if (!siblings.Exists(entry => entry.Path == path))
        {
            var entry = new FileEntry(name, path, FileEntryKind.Directory);
            siblings.Add(entry);
        }

        return this;
    }

    public InMemoryFileTree AddFile(string path)
    {
        var entry = new FileEntry(GetName(path), path, FileEntryKind.File);
        return AddEntry(entry);
    }

    public InMemoryFileTree AddEntry(FileEntry entry)
    {
        var parent = GetParent(entry.Path)!;
        AddDirectory(parent);
        _directories[parent].Add(entry);
        return this;
    }

    public InMemoryFileTree Deny(string path)
    {
        AddDirectory(path);
        _failures[path] = FileError.PermissionDenied(path);
        return this;
    }

    public async Task<FileResult<IReadOnlyList<FileEntry>>> ListAsync(string directoryPath, CancellationToken cancellationToken)
    {
        ListedPaths.Add(directoryPath);
        if (BeforeList is not null)
        {
            await BeforeList(directoryPath, cancellationToken);
        }

        if (_failures.TryGetValue(directoryPath, out var failure))
        {
            return FileResult<IReadOnlyList<FileEntry>>.Failure(failure);
        }

        if (!_directories.TryGetValue(directoryPath, out var entries))
        {
            var missing = FileError.NotFound(directoryPath);
            return FileResult<IReadOnlyList<FileEntry>>.Failure(missing);
        }

        IReadOnlyList<FileEntry> snapshot = entries.ToList();
        return FileResult<IReadOnlyList<FileEntry>>.Success(snapshot);
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }

    private static string? GetParent(string path)
    {
        if (path == "/")
        {
            return null;
        }

        var separator = path.LastIndexOf('/');
        return separator <= 0 ? "/" : path[..separator];
    }

    private static string GetName(string path)
    {
        var separator = path.LastIndexOf('/');
        return path[(separator + 1)..];
    }
}

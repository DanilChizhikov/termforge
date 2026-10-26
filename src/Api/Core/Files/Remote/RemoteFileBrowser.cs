using DTech.Core.Connections;

namespace DTech.Core.Files.Remote;

public sealed class RemoteFileBrowser(IRemoteConnectionOpener opener, RemotePathNormalizer normalizer)
{
    public async Task<FileResult<DirectoryListing>> ListAsync(Guid connectionId, string? requestedPath, CancellationToken cancellationToken)
    {
        var requested = NormalizeRequested(requestedPath);
        if (!requested.IsSuccess)
        {
            return FileResult<DirectoryListing>.Failure(requested.Error);
        }

        var opened = await opener.OpenAsync(connectionId, cancellationToken);
        if (!opened.IsSuccess)
        {
            return FileResult<DirectoryListing>.Failure(opened.Error);
        }

        await using var client = opened.Value;
        var path = await ResolvePathAsync(client, requested.Value, cancellationToken);
        if (!path.IsSuccess)
        {
            return FileResult<DirectoryListing>.Failure(path.Error);
        }

        var listed = await client.ListAsync(path.Value, cancellationToken);
        if (!listed.IsSuccess)
        {
            var failure = await DescribeListFailureAsync(client, path.Value, listed.Error!, cancellationToken);
            return FileResult<DirectoryListing>.Failure(failure);
        }

        var entries = MapEntries(listed.Value);
        var parent = normalizer.GetParent(path.Value);
        var listing = new DirectoryListing(path.Value, parent, entries);
        return FileResult<DirectoryListing>.Success(listing);
    }

    public async Task<FileResult<FileEntry>> StatAsync(Guid connectionId, string? requestedPath, CancellationToken cancellationToken)
    {
        var normalized = normalizer.Normalize(requestedPath);
        if (!normalized.IsSuccess)
        {
            return FileResult<FileEntry>.Failure(normalized.Error);
        }

        var opened = await opener.OpenAsync(connectionId, cancellationToken);
        if (!opened.IsSuccess)
        {
            return FileResult<FileEntry>.Failure(opened.Error);
        }

        await using var client = opened.Value;
        var stat = await client.StatAsync(normalized.Value, cancellationToken);
        if (!stat.IsSuccess)
        {
            var failure = FileErrorMapper.FromSsh(stat.Error!, normalized.Value);
            return FileResult<FileEntry>.Failure(failure);
        }

        var entry = RemoteEntryMapper.Map(stat.Value);
        return FileResult<FileEntry>.Success(entry);
    }

    private FileResult<string?> NormalizeRequested(string? requestedPath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            return FileResult<string?>.Success(null);
        }

        var normalized = normalizer.Normalize(requestedPath);
        if (!normalized.IsSuccess)
        {
            return FileResult<string?>.Failure(normalized.Error);
        }

        return FileResult<string?>.Success(normalized.Value);
    }

    private async Task<FileResult<string>> ResolvePathAsync(IRemoteFileClient client, string? normalizedPath, CancellationToken cancellationToken)
    {
        if (normalizedPath is not null)
        {
            return FileResult<string>.Success(normalizedPath);
        }

        var home = await client.GetHomeDirectoryAsync(cancellationToken);
        if (!home.IsSuccess)
        {
            var failure = FileErrorMapper.FromSsh(home.Error!, "home directory");
            return FileResult<string>.Failure(failure);
        }

        return normalizer.Normalize(home.Value);
    }

    private static async Task<FileError> DescribeListFailureAsync(IRemoteFileClient client, string path, SshError error, CancellationToken cancellationToken)
    {
        if (error.Kind is not (SshErrorKind.OperationFailed or SshErrorKind.RemotePathNotFound))
        {
            return FileErrorMapper.FromSsh(error, path);
        }

        var stat = await client.StatAsync(path, cancellationToken);
        var isFile = stat.IsSuccess && IsNonDirectory(stat.Value);
        return isFile ? FileError.NotADirectory(path) : FileErrorMapper.FromSsh(error, path);
    }

    private static bool IsNonDirectory(RemoteFileEntry entry)
    {
        if (entry.Kind == RemoteFileKind.SymbolicLink)
        {
            return entry.LinkTargetKind is RemoteFileKind.File or RemoteFileKind.Other;
        }

        return entry.Kind != RemoteFileKind.Directory;
    }

    private static List<FileEntry> MapEntries(IReadOnlyList<RemoteFileEntry> remoteEntries)
    {
        var entries = new List<FileEntry>(remoteEntries.Count);
        for (var i = 0; i < remoteEntries.Count; i++)
        {
            var entry = RemoteEntryMapper.Map(remoteEntries[i]);
            entries.Add(entry);
        }

        return entries;
    }
}

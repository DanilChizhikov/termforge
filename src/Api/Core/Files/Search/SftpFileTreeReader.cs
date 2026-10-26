using DTech.Core.Files.Remote;

namespace DTech.Core.Files.Search;

public sealed class SftpFileTreeReader(IRemoteFileClient client) : IFileTreeReader
{
    public FileSearchMethod Method => FileSearchMethod.Sftp;

    public async Task<FileResult<IReadOnlyList<FileEntry>>> ListAsync(string directoryPath, CancellationToken cancellationToken)
    {
        var listed = await client.ListAsync(directoryPath, cancellationToken);
        if (!listed.IsSuccess)
        {
            var error = FileErrorMapper.FromSsh(listed.Error!, directoryPath);
            return FileResult<IReadOnlyList<FileEntry>>.Failure(error);
        }

        var remoteEntries = listed.Value;
        var entries = new List<FileEntry>(remoteEntries.Count);
        for (var i = 0; i < remoteEntries.Count; i++)
        {
            var entry = RemoteEntryMapper.Map(remoteEntries[i]);
            entries.Add(entry);
        }

        return FileResult<IReadOnlyList<FileEntry>>.Success(entries);
    }

    public ValueTask DisposeAsync()
    {
        return client.DisposeAsync();
    }
}

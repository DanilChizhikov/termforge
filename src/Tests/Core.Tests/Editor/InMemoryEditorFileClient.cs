using System.Text;
using DTech.Core.Connections;
using DTech.Core.Editor;
using DTech.Core.Files;

namespace DTech.Core.Tests.Editor;

internal sealed class InMemoryEditorFileClient : IRemoteEditorFileClient
{
    public const int OwnerId = 1000;
    public const int SessionUserId = 2000;

    private readonly Dictionary<string, StoredFile> _files = new(StringComparer.Ordinal);
    private DateTime _clock = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public Func<string, bool> FailCreate { get; set; } = static _ => false;

    public Func<RemoteRenameRequest, bool> FailRename { get; set; } = static _ => false;

    public bool FailAtomicReplace { get; set; }

    public bool AtomicReplaceUnsupported { get; set; }

    public bool FailChangeOwner { get; set; }

    public bool FailOverwrite { get; set; }

    public int CreateCount { get; private set; }

    public List<RemoteFileCreation> Creations { get; } = [];

    public void Seed(string path, string text, int mode)
    {
        var content = Encoding.UTF8.GetBytes(text);
        SeedBytes(path, content, mode);
    }

    public void SeedBytes(string path, byte[] content, int mode)
    {
        _files[path] = new StoredFile(content, mode, OwnerId, OwnerId, NextTime());
    }

    public string ReadText(string path)
    {
        return Encoding.UTF8.GetString(_files[path].Content);
    }

    public bool Exists(string path)
    {
        return _files.ContainsKey(path);
    }

    public int ModeOf(string path)
    {
        return _files[path].Mode;
    }

    public int UserIdOf(string path)
    {
        return _files[path].UserId;
    }

    public IReadOnlyList<string> NamesIn(string directory)
    {
        var names = new List<string>();
        foreach (var path in _files.Keys)
        {
            var separator = path.LastIndexOf('/');
            var parent = separator == 0 ? "/" : path[..separator];
            if (parent == directory)
            {
                var name = path[(separator + 1)..];
                names.Add(name);
            }
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    public Task<SshResult<RemoteFileAttributes>> GetAttributesAsync(string remotePath, CancellationToken cancellationToken)
    {
        if (!_files.TryGetValue(remotePath, out var file))
        {
            var missing = NotFound(remotePath);
            var failure = SshResult<RemoteFileAttributes>.Failure(missing);
            return Task.FromResult(failure);
        }

        var attributes = ToAttributes(file);
        var result = SshResult<RemoteFileAttributes>.Success(attributes);
        return Task.FromResult(result);
    }

    public Task<SshResult<byte[]>> ReadAsync(EditorReadRequest request, CancellationToken cancellationToken)
    {
        if (!_files.TryGetValue(request.Path, out var file))
        {
            var missing = NotFound(request.Path);
            var failure = SshResult<byte[]>.Failure(missing);
            return Task.FromResult(failure);
        }

        var length = (int)Math.Min(file.Content.LongLength, request.MaxBytes + 1);
        var content = file.Content[..length];
        var result = SshResult<byte[]>.Success(content);
        return Task.FromResult(result);
    }

    public Task<SshResult<RemoteFileAttributes>> CreateFileAsync(RemoteFileCreation creation, CancellationToken cancellationToken)
    {
        CreateCount++;
        Creations.Add(creation);
        if (_files.ContainsKey(creation.Path))
        {
            var exists = new SshError(SshErrorKind.RemotePathExists, $"{creation.Path} exists");
            var failure = SshResult<RemoteFileAttributes>.Failure(exists);
            return Task.FromResult(failure);
        }

        if (FailCreate(creation.Path))
        {
            var failed = new SshError(SshErrorKind.OperationFailed, "No space left on device");
            var failure = SshResult<RemoteFileAttributes>.Failure(failed);
            return Task.FromResult(failure);
        }

        var content = creation.Content.ToArray();
        var file = new StoredFile(content, creation.Mode, SessionUserId, SessionUserId, NextTime());
        _files[creation.Path] = file;
        var attributes = ToAttributes(file);
        var result = SshResult<RemoteFileAttributes>.Success(attributes);
        return Task.FromResult(result);
    }

    public Task<SshResult> OverwriteAsync(RemoteFileContent content, CancellationToken cancellationToken)
    {
        if (FailOverwrite || !_files.TryGetValue(content.Path, out var file))
        {
            var failed = new SshError(SshErrorKind.OperationFailed, "Write failed");
            var failure = SshResult.Failure(failed);
            return Task.FromResult(failure);
        }

        var bytes = content.Content.ToArray();
        _files[content.Path] = file with { Content = bytes, Modified = NextTime() };
        var result = SshResult.Success();
        return Task.FromResult(result);
    }

    public Task<SshResult> ChangeOwnerAsync(RemoteOwnership ownership, CancellationToken cancellationToken)
    {
        if (FailChangeOwner)
        {
            var denied = new SshError(SshErrorKind.PermissionDenied, "Operation not permitted");
            var failure = SshResult.Failure(denied);
            return Task.FromResult(failure);
        }

        var file = _files[ownership.Path];
        _files[ownership.Path] = file with { UserId = ownership.UserId, GroupId = ownership.GroupId };
        var result = SshResult.Success();
        return Task.FromResult(result);
    }

    public Task<SshResult<AtomicReplaceOutcome>> ReplaceAtomicallyAsync(RemoteRenameRequest request, CancellationToken cancellationToken)
    {
        if (AtomicReplaceUnsupported)
        {
            var unsupported = SshResult<AtomicReplaceOutcome>.Success(AtomicReplaceOutcome.Unsupported);
            return Task.FromResult(unsupported);
        }

        if (FailAtomicReplace)
        {
            var failed = new SshError(SshErrorKind.OperationFailed, "Rename failed");
            var failure = SshResult<AtomicReplaceOutcome>.Failure(failed);
            return Task.FromResult(failure);
        }

        Move(request);
        var result = SshResult<AtomicReplaceOutcome>.Success(AtomicReplaceOutcome.Replaced);
        return Task.FromResult(result);
    }

    public Task<SshResult> RenameAsync(RemoteRenameRequest request, CancellationToken cancellationToken)
    {
        if (_files.ContainsKey(request.ToPath))
        {
            var exists = new SshError(SshErrorKind.RemotePathExists, $"{request.ToPath} exists");
            var failure = SshResult.Failure(exists);
            return Task.FromResult(failure);
        }

        if (FailRename(request))
        {
            var failed = new SshError(SshErrorKind.OperationFailed, "Rename failed");
            var failure = SshResult.Failure(failed);
            return Task.FromResult(failure);
        }

        Move(request);
        var result = SshResult.Success();
        return Task.FromResult(result);
    }

    public Task<SshResult> DeleteFileAsync(string remotePath, CancellationToken cancellationToken)
    {
        if (!_files.Remove(remotePath))
        {
            var missing = NotFound(remotePath);
            var failure = SshResult.Failure(missing);
            return Task.FromResult(failure);
        }

        var result = SshResult.Success();
        return Task.FromResult(result);
    }

    public Task<SshResult<IReadOnlyList<string>>> ListNamesAsync(string remoteDirectory, CancellationToken cancellationToken)
    {
        var names = NamesIn(remoteDirectory);
        var result = SshResult<IReadOnlyList<string>>.Success(names);
        return Task.FromResult(result);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private void Move(RemoteRenameRequest request)
    {
        var file = _files[request.FromPath];
        _files.Remove(request.FromPath);
        _files[request.ToPath] = file with { Modified = NextTime() };
    }

    private DateTime NextTime()
    {
        _clock = _clock.AddSeconds(1);
        return _clock;
    }

    private static RemoteFileAttributes ToAttributes(StoredFile file)
    {
        return new RemoteFileAttributes(RemoteFileKind.File, file.Content.LongLength, file.Modified)
        {
            Mode = file.Mode,
            UserId = file.UserId,
            GroupId = file.GroupId,
        };
    }

    private static SshError NotFound(string path)
    {
        return new SshError(SshErrorKind.RemotePathNotFound, $"{path} not found");
    }

    private sealed record StoredFile(byte[] Content, int Mode, int UserId, int GroupId, DateTime Modified);
}

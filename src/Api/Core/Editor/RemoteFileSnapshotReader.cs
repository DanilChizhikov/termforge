using DTech.Core.Connections;
using DTech.Core.Files;

namespace DTech.Core.Editor;

public sealed class RemoteFileSnapshotReader(EditorLimits limits)
{
    public async Task<EditorResult<RemoteFileSnapshot?>> ReadAsync(IRemoteEditorFileClient client, string path, CancellationToken cancellationToken)
    {
        var attributes = await client.GetAttributesAsync(path, cancellationToken);
        if (!attributes.IsSuccess)
        {
            return MissingOrFailure(attributes.Error!, path);
        }

        var unsupported = CheckEditable(attributes.Value, path);
        if (unsupported is not null)
        {
            return EditorResult<RemoteFileSnapshot?>.Failure(unsupported);
        }

        var request = new RemoteReadRequest(path, limits.MaxFileBytes);
        var read = await client.ReadAsync(request, cancellationToken);
        if (!read.IsSuccess)
        {
            return MissingOrFailure(read.Error!, path);
        }

        var content = read.Value;
        if (content.LongLength > limits.MaxFileBytes)
        {
            var tooLarge = EditorError.TooLarge(path, limits.MaxFileBytes);
            return EditorResult<RemoteFileSnapshot?>.Failure(tooLarge);
        }

        var version = RemoteFileVersion.Of(attributes.Value, content);
        var snapshot = new RemoteFileSnapshot(attributes.Value, content, version);
        return EditorResult<RemoteFileSnapshot?>.Success(snapshot);
    }

    private EditorError? CheckEditable(RemoteFileAttributes attributes, string path)
    {
        if (attributes.Kind == RemoteFileKind.SymbolicLink)
        {
            return EditorError.SymbolicLink(path);
        }

        if (attributes.Kind != RemoteFileKind.File)
        {
            return EditorError.NotAFile(path);
        }

        return attributes.Size > limits.MaxFileBytes ? EditorError.TooLarge(path, limits.MaxFileBytes) : null;
    }

    private static EditorResult<RemoteFileSnapshot?> MissingOrFailure(SshError error, string path)
    {
        if (error.Kind == SshErrorKind.RemotePathNotFound)
        {
            return EditorResult<RemoteFileSnapshot?>.Success(null);
        }

        var failure = EditorErrorMapper.FromSsh(error, path);
        return EditorResult<RemoteFileSnapshot?>.Failure(failure);
    }
}

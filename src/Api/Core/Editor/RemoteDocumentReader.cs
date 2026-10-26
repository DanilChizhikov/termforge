namespace DTech.Core.Editor;

public sealed class RemoteDocumentReader(RemoteFileSnapshotReader snapshots, TextContentCodec codec)
{
    public async Task<EditorResult<RemoteDocument>> ReadAsync(IRemoteEditorFileClient client, string path, CancellationToken cancellationToken)
    {
        var snapshot = await snapshots.ReadAsync(client, path, cancellationToken);
        if (!snapshot.IsSuccess)
        {
            return EditorResult<RemoteDocument>.Failure(snapshot.Error);
        }

        if (snapshot.Value is null)
        {
            var missing = EditorError.NotFound(path);
            return EditorResult<RemoteDocument>.Failure(missing);
        }

        var current = snapshot.Value;
        var decoded = codec.Decode(current.Content, path);
        if (!decoded.IsSuccess)
        {
            return EditorResult<RemoteDocument>.Failure(decoded.Error);
        }

        var document = new RemoteDocument(path, decoded.Value, current.Version, current.Attributes.Mode);
        return EditorResult<RemoteDocument>.Success(document);
    }
}

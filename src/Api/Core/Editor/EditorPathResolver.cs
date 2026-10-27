using DTech.Core.Files.Remote;

namespace DTech.Core.Editor;

public sealed class EditorPathResolver(RemotePathNormalizer normalizer)
{
    public EditorResult<RemoteFileLocation> Resolve(string? rawPath)
    {
        var normalized = normalizer.Normalize(rawPath);
        if (!normalized.IsSuccess)
        {
            var invalid = EditorErrorMapper.FromFile(normalized.Error);
            return EditorResult<RemoteFileLocation>.Failure(invalid);
        }

        var path = normalized.Value;
        var directory = normalizer.GetParent(path);
        if (directory is null)
        {
            var root = EditorError.Invalid("The filesystem root is not a file.");
            return EditorResult<RemoteFileLocation>.Failure(root);
        }

        var name = path[(path.LastIndexOf('/') + 1)..];
        var location = new RemoteFileLocation(directory, name);
        return EditorResult<RemoteFileLocation>.Success(location);
    }
}

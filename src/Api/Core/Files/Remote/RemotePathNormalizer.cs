namespace DTech.Core.Files.Remote;

public sealed class RemotePathNormalizer
{
    public const string RootPath = "/";

    public FileResult<string> Normalize(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            var empty = FileError.Invalid("Path must not be empty.");
            return FileResult<string>.Failure(empty);
        }

        if (!rawPath.StartsWith('/'))
        {
            var relative = FileError.Invalid("Remote path must be absolute.");
            return FileResult<string>.Failure(relative);
        }

        if (ContainsControlCharacter(rawPath))
        {
            var control = FileError.Invalid("Path must not contain control characters.");
            return FileResult<string>.Failure(control);
        }

        return Collapse(rawPath);
    }

    public string? GetParent(string normalizedPath)
    {
        if (normalizedPath == RootPath)
        {
            return null;
        }

        var lastSeparator = normalizedPath.LastIndexOf('/');
        return lastSeparator <= 0 ? RootPath : normalizedPath[..lastSeparator];
    }

    public string Combine(string normalizedParent, string name)
    {
        return normalizedParent == RootPath ? $"/{name}" : $"{normalizedParent}/{name}";
    }

    private static FileResult<string> Collapse(string rawPath)
    {
        var segments = rawPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var kept = new List<string>(segments.Length);
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (segment == "..")
            {
                var traversal = FileError.Invalid("Path must not contain '..' segments.");
                return FileResult<string>.Failure(traversal);
            }

            if (segment != ".")
            {
                kept.Add(segment);
            }
        }

        var normalized = kept.Count == 0 ? RootPath : $"/{string.Join('/', kept)}";
        return FileResult<string>.Success(normalized);
    }

    private static bool ContainsControlCharacter(string path)
    {
        for (var i = 0; i < path.Length; i++)
        {
            if (char.IsControl(path[i]))
            {
                return true;
            }
        }

        return false;
    }
}

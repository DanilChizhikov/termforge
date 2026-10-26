namespace DTech.Core.Files.Local;

public sealed class LocalPathResolver
{
    private static readonly string[] DeviceNamespacePrefixes = [@"\\?\", @"\\.\"];

    public FileResult<string> Resolve(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            var empty = FileError.Invalid("Path must not be empty.");
            return FileResult<string>.Failure(empty);
        }

        var rejection = FindRejection(rawPath);
        if (rejection is not null)
        {
            return FileResult<string>.Failure(rejection);
        }

        var fullPath = Path.GetFullPath(rawPath);
        var normalized = Path.TrimEndingDirectorySeparator(fullPath);
        return FileResult<string>.Success(normalized);
    }

    private static FileError? FindRejection(string rawPath)
    {
        if (ContainsControlCharacter(rawPath))
        {
            return FileError.Invalid("Path must not contain control characters.");
        }

        if (StartsWithDeviceNamespace(rawPath))
        {
            return FileError.Invalid("Device paths are not allowed.");
        }

        if (!Path.IsPathFullyQualified(rawPath))
        {
            return FileError.Invalid("Path must be absolute.");
        }

        if (HasParentSegment(rawPath))
        {
            return FileError.Invalid("Path must not contain '..' segments.");
        }

        return null;
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

    private static bool StartsWithDeviceNamespace(string path)
    {
        for (var i = 0; i < DeviceNamespacePrefixes.Length; i++)
        {
            if (path.StartsWith(DeviceNamespacePrefixes[i], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasParentSegment(string path)
    {
        var segments = path.Split('/', '\\');
        for (var i = 0; i < segments.Length; i++)
        {
            if (segments[i] == "..")
            {
                return true;
            }
        }

        return false;
    }
}

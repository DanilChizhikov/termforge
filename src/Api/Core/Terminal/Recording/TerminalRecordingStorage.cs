namespace DTech.Core.Terminal.Recording;

public sealed class TerminalRecordingStorage(TerminalRecordingStorageOptions options)
{
    private const string MetadataExtension = ".json";
    private const string MetadataSearchPattern = "*" + MetadataExtension;
    private const string TemporaryExtension = ".tmp";
    private const UnixFileMode PrivateDirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode PrivateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly string _directoryPath = ResolveDirectory(options.DirectoryPath);

    public string DirectoryPath => _directoryPath;

    public Stream CreateCast(TerminalRecordingId id)
    {
        EnsureDirectory();
        var path = CastPath(id);
        var streamOptions = CreateWriteOptions(FileMode.CreateNew);
        return new FileStream(path, streamOptions);
    }

    public Stream? OpenCast(TerminalRecordingId id)
    {
        var path = CastPath(id);
        if (!File.Exists(path))
        {
            return null;
        }

        var streamOptions = new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.ReadWrite | FileShare.Delete,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
        };
        return new FileStream(path, streamOptions);
    }

    public async Task WriteMetadataAsync(TerminalRecordingMetadata metadata, CancellationToken cancellationToken)
    {
        EnsureDirectory();
        var json = TerminalRecordingMetadataSerializer.Serialize(metadata);
        var path = MetadataPath(metadata.Id);
        var temporaryPath = path + TemporaryExtension;
        var streamOptions = CreateWriteOptions(FileMode.Create);
        await using (var stream = new FileStream(temporaryPath, streamOptions))
        {
            await stream.WriteAsync(json, cancellationToken);
        }

        File.Move(temporaryPath, path, true);
    }

    public async Task<TerminalRecordingMetadata?> FindAsync(TerminalRecordingId id, CancellationToken cancellationToken)
    {
        var path = MetadataPath(id);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllBytesAsync(path, cancellationToken);
            return TerminalRecordingMetadataSerializer.Deserialize(json, id);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<TerminalRecordingMetadata>> ListAsync(CancellationToken cancellationToken)
    {
        var recordings = new List<TerminalRecordingMetadata>();
        if (!Directory.Exists(_directoryPath))
        {
            return recordings;
        }

        foreach (var path in Directory.EnumerateFiles(_directoryPath, MetadataSearchPattern))
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (!TerminalRecordingId.TryParse(fileName, out var id))
            {
                continue;
            }

            var metadata = await FindAsync(id, cancellationToken);
            if (metadata is not null)
            {
                recordings.Add(metadata);
            }
        }

        return recordings;
    }

    public bool Delete(TerminalRecordingId id)
    {
        var castPath = CastPath(id);
        var metadataPath = MetadataPath(id);
        var existed = File.Exists(metadataPath) || File.Exists(castPath);
        File.Delete(castPath);
        File.Delete(metadataPath);
        return existed;
    }

    private string CastPath(TerminalRecordingId id)
    {
        return ResolveInsideDirectory(id.Value + AsciicastFormat.FileExtension);
    }

    private string MetadataPath(TerminalRecordingId id)
    {
        return ResolveInsideDirectory(id.Value + MetadataExtension);
    }

    private string ResolveInsideDirectory(string fileName)
    {
        var combinedPath = Path.Combine(_directoryPath, fileName);
        var path = Path.GetFullPath(combinedPath);
        var parent = Path.GetDirectoryName(path);
        if (!string.Equals(parent, _directoryPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Recording file {fileName} resolves outside the recordings directory.");
        }

        return path;
    }

    private void EnsureDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(_directoryPath);
            return;
        }

        Directory.CreateDirectory(_directoryPath, PrivateDirectoryMode);
    }

    private static string ResolveDirectory(string configuredPath)
    {
        var fullPath = Path.GetFullPath(configuredPath);
        return Path.TrimEndingDirectorySeparator(fullPath);
    }

    private static FileStreamOptions CreateWriteOptions(FileMode mode)
    {
        var streamOptions = new FileStreamOptions
        {
            Mode = mode,
            Access = FileAccess.Write,
            Share = FileShare.Read | FileShare.Delete,
            Options = FileOptions.Asynchronous,
        };
        if (!OperatingSystem.IsWindows())
        {
            streamOptions.UnixCreateMode = PrivateFileMode;
        }

        return streamOptions;
    }
}

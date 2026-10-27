using Microsoft.Extensions.Options;

namespace DTech.Credentials.Vault;

public sealed class VaultFile
{
    private const string ApplicationDirectoryName = "termforge";
    private const string FileName = "credentials.vault";
    private const UnixFileMode OwnerOnlyFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode OwnerOnlyDirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    public VaultFile(IOptions<CredentialStoreOptions> options)
    {
        var configuredPath = options.Value.VaultPath;
        FilePath = string.IsNullOrWhiteSpace(configuredPath) ? GetDefaultPath() : Path.GetFullPath(configuredPath);
    }

    public string FilePath { get; }

    public bool Exists => File.Exists(FilePath);

    public async Task<byte[]?> Read(CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllBytesAsync(FilePath, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    public async Task Write(byte[] content, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(FilePath)!;
        CreateDirectory(directory);
        var temporaryPath = $"{FilePath}.{Guid.NewGuid():N}.tmp";
        var streamOptions = CreateStreamOptions();
        try
        {
            await using (var stream = new FileStream(temporaryPath, streamOptions))
            {
                await stream.WriteAsync(content, cancellationToken);
                stream.Flush(true);
            }

            File.Move(temporaryPath, FilePath, true);
        }
        catch
        {
            File.Delete(temporaryPath);
            throw;
        }
    }

    private static void CreateDirectory(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(directory);
            return;
        }

        Directory.CreateDirectory(directory, OwnerOnlyDirectoryMode);
    }

    private static FileStreamOptions CreateStreamOptions()
    {
        var streamOptions = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.WriteThrough,
        };
        if (!OperatingSystem.IsWindows())
        {
            streamOptions.UnixCreateMode = OwnerOnlyFileMode;
        }

        return streamOptions;
    }

    private static string GetDefaultPath()
    {
        var applicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(applicationData))
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, $".{ApplicationDirectoryName}", FileName);
        }

        return Path.Combine(applicationData, ApplicationDirectoryName, FileName);
    }
}

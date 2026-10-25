namespace DTech.Ssh.IntegrationTests.Harness;

internal static class TestKeyFiles
{
    public static string CreateDirectory()
    {
        var directoryName = $"tf-ssh-{Guid.NewGuid():N}"[..14];
        var directoryPath = Path.Combine(Path.GetTempPath(), directoryName);
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    public static async Task WritePrivateKeyAsync(string path, byte[] content, CancellationToken cancellationToken)
    {
        await File.WriteAllBytesAsync(path, content, cancellationToken);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    public static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }
}

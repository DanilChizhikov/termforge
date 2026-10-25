namespace DTech.Credentials.Tests.Support;

public sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        var name = $"termforge-credentials-tests-{Guid.NewGuid():N}";
        DirectoryPath = Path.Combine(Path.GetTempPath(), name);
        Directory.CreateDirectory(DirectoryPath);
    }

    public string DirectoryPath { get; }

    public string Combine(string fileName)
    {
        return Path.Combine(DirectoryPath, fileName);
    }

    public void Dispose()
    {
        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, true);
        }
    }
}

namespace DTech.Core.Tests.SavedConnections.Support;

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"termforge-core-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(string relativePath)
    {
        return System.IO.Path.Combine(Path, relativePath);
    }

    public string Write(string relativePath, string content)
    {
        var fullPath = Combine(relativePath);
        var directory = System.IO.Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, true);
        }
    }
}

using DTech.Core.Files.Local;
using DTech.Core.Tests.SavedConnections.Support;

namespace DTech.Core.Tests.Files.Local;

internal sealed class LocalFileSystemFixture : IDisposable
{
    public LocalFileSystemFixture()
    {
        Directory = new TemporaryDirectory();
        var pathResolver = new LocalPathResolver();
        var rootsProvider = new LocalRootsProvider();
        var entryFactory = new LocalEntryFactory();
        Browser = new LocalFileBrowser(pathResolver, rootsProvider, entryFactory);
        Operations = new LocalFileOperations(pathResolver);
    }

    public TemporaryDirectory Directory { get; }

    public LocalFileBrowser Browser { get; }

    public LocalFileOperations Operations { get; }

    public bool TryCreateSymbolicLink(string linkName, string target)
    {
        var linkPath = Directory.Combine(linkName);
        try
        {
            if (System.IO.Directory.Exists(target))
            {
                System.IO.Directory.CreateSymbolicLink(linkPath, target);
            }
            else
            {
                File.CreateSymbolicLink(linkPath, target);
            }

            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        Directory.Dispose();
    }
}

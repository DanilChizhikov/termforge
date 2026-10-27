namespace DTech.Entrypoint.SpaHosting;

internal static class PublishedContentRoot
{
    private const string WebRootDirectoryName = "wwwroot";
    private const string IndexFileName = "index.html";

    public static string? Find(string applicationDirectory)
    {
        var indexPath = Path.Combine(applicationDirectory, WebRootDirectoryName, IndexFileName);
        return File.Exists(indexPath) ? applicationDirectory : null;
    }
}

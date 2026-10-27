namespace DTech.Core.Editor;

public sealed record RemoteFileLocation(string Directory, string Name)
{
    private const string RootPath = "/";

    public string Path => Sibling(Name);

    public string Sibling(string name)
    {
        return Directory == RootPath ? $"/{name}" : $"{Directory}/{name}";
    }
}

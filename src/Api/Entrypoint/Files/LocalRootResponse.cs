using DTech.Core.Files.Local;

namespace DTech.Entrypoint.Files;

public sealed record LocalRootResponse(string Name, string Path)
{
    public static LocalRootResponse From(LocalRoot root)
    {
        return new LocalRootResponse(root.Name, root.Path);
    }
}

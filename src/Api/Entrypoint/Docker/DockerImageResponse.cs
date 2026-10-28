using DTech.Core.Docker;

namespace DTech.Entrypoint.Docker;

internal sealed record DockerImageResponse(string Id, string Repository, string Tag, string Size, string CreatedSince)
{
    public static DockerImageResponse From(DockerImage image)
    {
        return new DockerImageResponse(image.Id, image.Repository, image.Tag, image.Size, image.CreatedSince);
    }
}

using DTech.Core.Docker;

namespace DTech.Entrypoint.Docker;

internal sealed record DockerImagesResponse(Guid ConnectionId, string ServerName, IReadOnlyList<DockerImageResponse> Images)
{
    public static DockerImagesResponse From(DockerImageList list)
    {
        var images = new List<DockerImageResponse>(list.Images.Count);
        for (var i = 0; i < list.Images.Count; i++)
        {
            var image = DockerImageResponse.From(list.Images[i]);
            images.Add(image);
        }

        return new DockerImagesResponse(list.ConnectionId, list.ServerName, images);
    }
}

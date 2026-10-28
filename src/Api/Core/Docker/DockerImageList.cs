namespace DTech.Core.Docker;

public sealed record DockerImageList(Guid ConnectionId, string ServerName, IReadOnlyList<DockerImage> Images);

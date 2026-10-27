namespace DTech.Core.Docker;

public sealed record ContainerLogRequest(Guid ConnectionId, string? Container, ContainerLogOptions Options);

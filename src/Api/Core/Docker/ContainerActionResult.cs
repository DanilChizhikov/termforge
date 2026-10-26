namespace DTech.Core.Docker;

public sealed record ContainerActionResult(Guid OperationId, string Container, DockerContainerAction Action);

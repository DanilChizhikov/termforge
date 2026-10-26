namespace DTech.Entrypoint.Docker;

internal sealed record ContainerActionResponse(Guid OperationId, string Container, string Action);

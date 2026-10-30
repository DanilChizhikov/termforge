namespace DTech.Entrypoint.Docker;

internal sealed record ComposeActionResponse(Guid OperationId, string Project, string Action);

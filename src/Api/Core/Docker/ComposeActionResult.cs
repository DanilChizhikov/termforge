namespace DTech.Core.Docker;

public sealed record ComposeActionResult(Guid OperationId, string Project, ComposeAction Action);

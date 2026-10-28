namespace DTech.Entrypoint.Docker;

internal sealed record ImageRemoveResponse(Guid OperationId, string Image);

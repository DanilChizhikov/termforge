namespace DTech.Entrypoint.Operations;

public sealed record OperationPageResponse(IReadOnlyList<OperationResponse> Items, int Total, int Offset, int Limit);

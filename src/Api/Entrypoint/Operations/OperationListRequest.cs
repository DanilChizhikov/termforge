namespace DTech.Entrypoint.Operations;

public sealed record OperationListRequest(
    string? Kind,
    string? Status,
    string? Server,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int? Offset,
    int? Limit);

namespace DTech.Entrypoint.Transfers;

public sealed record CreateTransfersBody(Guid? ConnectionId, string? Direction, IReadOnlyList<string>? SourcePaths, string? TargetDirectory, bool Overwrite, string? ConfirmationToken);

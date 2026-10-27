namespace DTech.Entrypoint.Safety;

public sealed record ConfirmationBody(string? Operation, Guid? ConnectionId, string? Target);

namespace DTech.Core.AiBridge.Commands;

public readonly record struct AiCommandRequest(Guid ConnectionId, string? Command, int? TimeoutSeconds);

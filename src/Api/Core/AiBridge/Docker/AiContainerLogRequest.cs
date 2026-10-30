namespace DTech.Core.AiBridge.Docker;

public readonly record struct AiContainerLogRequest(Guid ConnectionId, string? Container, int? Tail);

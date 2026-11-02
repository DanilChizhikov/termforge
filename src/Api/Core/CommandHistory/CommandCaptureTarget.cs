namespace DTech.Core.CommandHistory;

public readonly record struct CommandCaptureTarget(Guid ConnectionId, string Nonce);

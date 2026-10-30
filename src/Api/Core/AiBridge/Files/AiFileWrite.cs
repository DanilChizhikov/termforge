namespace DTech.Core.AiBridge.Files;

public sealed record AiFileWrite(Guid ConnectionId, string? Path, string? Content, string? ExpectedSha256);

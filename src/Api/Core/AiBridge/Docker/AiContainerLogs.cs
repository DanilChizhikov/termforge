namespace DTech.Core.AiBridge.Docker;

public sealed record AiContainerLogs(string Container, string Text, bool Truncated);

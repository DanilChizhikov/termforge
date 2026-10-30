namespace DTech.Core.Docker;

public sealed record DockerOperationEntry(string Kind, string Server, IReadOnlyDictionary<string, string> Details);

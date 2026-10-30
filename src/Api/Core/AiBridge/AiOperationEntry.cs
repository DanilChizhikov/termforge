namespace DTech.Core.AiBridge;

public readonly record struct AiOperationEntry(string Kind, string Server, IReadOnlyDictionary<string, string> Details);

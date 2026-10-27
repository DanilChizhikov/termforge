namespace DTech.Entrypoint.CommandHistory;

internal sealed record ExclusionPatternsRequest(IReadOnlyList<string>? Patterns);

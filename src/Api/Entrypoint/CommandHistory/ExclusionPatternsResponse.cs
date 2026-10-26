namespace DTech.Entrypoint.CommandHistory;

internal sealed record ExclusionPatternsResponse(IReadOnlyList<string> Patterns, IReadOnlyList<string> Defaults);

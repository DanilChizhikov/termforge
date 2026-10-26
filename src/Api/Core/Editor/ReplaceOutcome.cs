namespace DTech.Core.Editor;

public sealed record ReplaceOutcome(SaveStrategy Strategy, IReadOnlyList<string> Warnings);

namespace DTech.Core.SavedConnections;

public sealed record SavedConnectionSubmission(SavedConnectionDraft Draft, string? Password)
{
    public override string ToString()
    {
        return $"{nameof(SavedConnectionSubmission)} {{ {nameof(Draft)} = {Draft} }}";
    }
}

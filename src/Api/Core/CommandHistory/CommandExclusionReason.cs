namespace DTech.Core.CommandHistory;

public enum CommandExclusionReason
{
    Empty,
    LeadingSpace,
    ControlCharacters,
    TooLong,
    MatchesPattern,
}

using System.Text.RegularExpressions;

namespace DTech.Core.CommandHistory;

public sealed class CommandExclusionRules
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    private readonly IReadOnlyList<Regex> _patterns;

    private CommandExclusionRules(IReadOnlyList<Regex> patterns)
    {
        _patterns = patterns;
    }

    public static CommandExclusionRules Create(IReadOnlyList<string> patterns)
    {
        var compiled = new List<Regex>(patterns.Count);
        for (var i = 0; i < patterns.Count; i++)
        {
            var regex = TryCompile(patterns[i]);
            if (regex is not null)
            {
                compiled.Add(regex);
            }
        }

        return new CommandExclusionRules(compiled);
    }

    public static string? Validate(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) || pattern.Length > CommandHistoryLimits.MaxExclusionPatternLength)
        {
            return $"A pattern must be 1 to {CommandHistoryLimits.MaxExclusionPatternLength} characters long.";
        }

        try
        {
            _ = new Regex(pattern, RegexOptions.CultureInvariant, MatchTimeout);
            return null;
        }
        catch (ArgumentException exception)
        {
            return $"The pattern '{pattern}' is not a valid regular expression: {exception.Message}";
        }
    }

    public CommandExclusionReason? FindReason(string command)
    {
        if (command.Trim().Length == 0)
        {
            return CommandExclusionReason.Empty;
        }

        if (char.IsWhiteSpace(command[0]))
        {
            return CommandExclusionReason.LeadingSpace;
        }

        if (command.Length > CommandHistoryLimits.MaxCommandLength)
        {
            return CommandExclusionReason.TooLong;
        }

        if (ContainsControlCharacters(command))
        {
            return CommandExclusionReason.ControlCharacters;
        }

        return MatchesAnyPattern(command) ? CommandExclusionReason.MatchesPattern : null;
    }

    private bool MatchesAnyPattern(string command)
    {
        for (var i = 0; i < _patterns.Count; i++)
        {
            try
            {
                if (_patterns[i].IsMatch(command))
                {
                    return true;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsControlCharacters(string command)
    {
        for (var i = 0; i < command.Length; i++)
        {
            var character = command[i];
            if (char.IsControl(character) && character != '\t' && character != '\n')
            {
                return true;
            }
        }

        return false;
    }

    private static Regex? TryCompile(string pattern)
    {
        try
        {
            return new Regex(pattern, RegexOptions.CultureInvariant, MatchTimeout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}

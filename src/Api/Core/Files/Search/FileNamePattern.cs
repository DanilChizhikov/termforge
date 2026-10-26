using System.Text;

namespace DTech.Core.Files.Search;

public sealed class FileNamePattern
{
    public const int MaxLength = 255;

    private const char AnySequence = '*';
    private const char AnyCharacter = '?';

    private FileNamePattern(string text, string glob)
    {
        Text = text;
        Glob = glob;
    }

    public string Text { get; }

    public string Glob { get; }

    public static FileResult<FileNamePattern> Create(string? rawPattern)
    {
        var text = rawPattern?.Trim() ?? string.Empty;
        var rejection = FindRejection(text);
        if (rejection is not null)
        {
            var error = FileError.Invalid(rejection);
            return FileResult<FileNamePattern>.Failure(error);
        }

        var glob = HasWildcard(text) ? text : $"{AnySequence}{text}{AnySequence}";
        var pattern = new FileNamePattern(text, glob);
        return FileResult<FileNamePattern>.Success(pattern);
    }

    public bool IsMatch(string name)
    {
        var nameIndex = 0;
        var globIndex = 0;
        var starGlobIndex = -1;
        var starNameIndex = 0;
        while (nameIndex < name.Length)
        {
            if (globIndex < Glob.Length && Glob[globIndex] == AnySequence)
            {
                starGlobIndex = globIndex;
                starNameIndex = nameIndex;
                globIndex++;
                continue;
            }

            if (globIndex < Glob.Length && CharactersMatch(Glob[globIndex], name[nameIndex]))
            {
                globIndex++;
                nameIndex++;
                continue;
            }

            if (starGlobIndex < 0)
            {
                return false;
            }

            starNameIndex++;
            nameIndex = starNameIndex;
            globIndex = starGlobIndex + 1;
        }

        while (globIndex < Glob.Length && Glob[globIndex] == AnySequence)
        {
            globIndex++;
        }

        return globIndex == Glob.Length;
    }

    public string ToFindGlob()
    {
        var builder = new StringBuilder(Glob.Length);
        for (var i = 0; i < Glob.Length; i++)
        {
            var character = Glob[i];
            if (character is '[' or ']')
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static bool CharactersMatch(char globCharacter, char nameCharacter)
    {
        if (globCharacter == AnyCharacter)
        {
            return true;
        }

        return char.ToUpperInvariant(globCharacter) == char.ToUpperInvariant(nameCharacter);
    }

    private static bool HasWildcard(string text)
    {
        return text.Contains(AnySequence, StringComparison.Ordinal) || text.Contains(AnyCharacter, StringComparison.Ordinal);
    }

    private static string? FindRejection(string text)
    {
        if (text.Length == 0)
        {
            return "Search pattern must not be empty.";
        }

        if (text.Length > MaxLength)
        {
            return $"Search pattern must be at most {MaxLength} characters.";
        }

        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            if (char.IsControl(character))
            {
                return "Search pattern must not contain control characters.";
            }

            if (character is '/' or '\\')
            {
                return "Search pattern matches file names only and must not contain path separators.";
            }
        }

        return null;
    }
}

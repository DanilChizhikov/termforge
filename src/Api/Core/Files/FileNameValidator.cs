namespace DTech.Core.Files;

public static class FileNameValidator
{
    private const int MaxNameLength = 255;

    public static FileError? Validate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return FileError.Invalid("Name must not be empty.");
        }

        if (name.Length > MaxNameLength)
        {
            return FileError.Invalid($"Name must not be longer than {MaxNameLength} characters.");
        }

        if (name is "." or "..")
        {
            return FileError.Invalid("Name must not be '.' or '..'.");
        }

        if (ContainsForbiddenCharacter(name))
        {
            return FileError.Invalid("Name must not contain path separators or control characters.");
        }

        return null;
    }

    private static bool ContainsForbiddenCharacter(string name)
    {
        for (var i = 0; i < name.Length; i++)
        {
            var character = name[i];
            if (character is '/' or '\\' || char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }
}

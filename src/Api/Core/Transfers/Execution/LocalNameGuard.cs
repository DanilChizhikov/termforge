using DTech.Core.Files;

namespace DTech.Core.Transfers.Execution;

public static class LocalNameGuard
{
    private static readonly char[] InvalidCharacters = Path.GetInvalidFileNameChars();

    public static FileError? Validate(string name)
    {
        var nameError = FileNameValidator.Validate(name);
        if (nameError is not null)
        {
            return nameError;
        }

        if (name.IndexOfAny(InvalidCharacters) >= 0)
        {
            return FileError.Invalid($"Name cannot be stored on this computer: {name}");
        }

        return null;
    }
}

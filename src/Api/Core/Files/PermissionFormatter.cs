namespace DTech.Core.Files;

public static class PermissionFormatter
{
    private const string Symbols = "rwxrwxrwx";

    public static string Format(UnixFileMode mode)
    {
        var characters = new char[Symbols.Length];
        for (var i = 0; i < Symbols.Length; i++)
        {
            var bit = 1 << (Symbols.Length - 1 - i);
            var isSet = ((int)mode & bit) != 0;
            characters[i] = isSet ? Symbols[i] : '-';
        }

        return new string(characters);
    }
}

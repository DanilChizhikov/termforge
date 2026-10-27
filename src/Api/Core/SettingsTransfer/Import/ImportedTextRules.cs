namespace DTech.Core.SettingsTransfer.Import;

public static class ImportedTextRules
{
    public static bool HasControlCharacters(string? value)
    {
        if (value is null)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsControl(value[i]))
            {
                return true;
            }
        }

        return false;
    }

    public static bool StartsLikeOption(string? value)
    {
        return value is not null && value.StartsWith('-');
    }
}

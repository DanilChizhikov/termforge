namespace DTech.Core.Editor.Sudo;

public static class FileAccessOutputParser
{
    private const string WritableFlag = "1";
    private const string ReadOnlyFlag = "0";
    private const int MaxOwnerNameLength = 64;

    public static RemoteFileAccess? Parse(string output, int ownerId)
    {
        var lines = output.Split('\n');
        var flag = lines[0].Trim();
        if (flag is not (WritableFlag or ReadOnlyFlag))
        {
            return null;
        }

        var ownerName = lines.Length > 1 ? Sanitize(lines[1]) : null;
        return new RemoteFileAccess(ownerId, ownerName, flag == WritableFlag);
    }

    private static string? Sanitize(string line)
    {
        var name = line.Trim();
        if (name.Length == 0 || name.Length > MaxOwnerNameLength)
        {
            return null;
        }

        for (var i = 0; i < name.Length; i++)
        {
            var character = name[i];
            if (char.IsControl(character) || char.IsWhiteSpace(character))
            {
                return null;
            }
        }

        return name;
    }
}

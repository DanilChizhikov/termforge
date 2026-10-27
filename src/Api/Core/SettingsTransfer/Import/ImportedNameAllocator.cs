using DTech.Core.SavedConnections;

namespace DTech.Core.SettingsTransfer.Import;

public static class ImportedNameAllocator
{
    private const string Suffix = " (imported)";

    public static string Allocate(string name, HashSet<string> takenNames)
    {
        for (var attempt = 1; ; attempt++)
        {
            var suffix = attempt == 1 ? Suffix : $" (imported {attempt})";
            var maxBaseLength = SavedConnectionValidator.MaxNameLength - suffix.Length;
            var baseName = name.Length > maxBaseLength ? name[..maxBaseLength].TrimEnd() : name;
            var candidate = baseName + suffix;
            if (takenNames.Add(candidate))
            {
                return candidate;
            }
        }
    }
}

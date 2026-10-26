namespace DTech.Core.Updates;

public sealed record UpdateStatus(
    UpdateState State,
    string CurrentVersion,
    string? LatestVersion,
    string? ReleaseUrl,
    DateTime? CheckedAt)
{
    public static UpdateStatus NotChecked(string currentVersion)
    {
        return new UpdateStatus(UpdateState.NotChecked, currentVersion, null, null, null);
    }
}

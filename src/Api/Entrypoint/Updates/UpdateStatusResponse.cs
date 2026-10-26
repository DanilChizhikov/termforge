using DTech.Core.Updates;

namespace DTech.Entrypoint.Updates;

public sealed record UpdateStatusResponse(
    string State,
    string CurrentVersion,
    string? LatestVersion,
    string? ReleaseUrl,
    DateTime? CheckedAt)
{
    public static UpdateStatusResponse From(UpdateStatus status)
    {
        var state = status.State.ToString().ToLowerInvariant();
        return new UpdateStatusResponse(state, status.CurrentVersion, status.LatestVersion, status.ReleaseUrl, status.CheckedAt);
    }
}

using DTech.Core.Updates;

namespace DTech.Entrypoint.Updates;

internal static class GitHubReleaseSelector
{
    private const string TrustedReleaseHost = "github.com";

    public static ReleaseInfo? SelectLatest(IReadOnlyList<GitHubReleaseDto> releases)
    {
        ReleaseInfo? latest = null;
        for (var i = 0; i < releases.Count; i++)
        {
            var candidate = ToPublishedRelease(releases[i]);
            if (candidate is null)
            {
                continue;
            }

            if (latest is null || candidate.Version.CompareTo(latest.Version) > 0)
            {
                latest = candidate;
            }
        }

        return latest;
    }

    private static ReleaseInfo? ToPublishedRelease(GitHubReleaseDto release)
    {
        if (release is null || release.Draft || release.Prerelease)
        {
            return null;
        }

        var parsed = SemanticVersion.TryParse(release.TagName, out var version);
        if (!parsed || version!.IsPreRelease)
        {
            return null;
        }

        var urlValid = Uri.TryCreate(release.HtmlUrl, UriKind.Absolute, out var url);
        if (!urlValid || url!.Scheme != Uri.UriSchemeHttps || !url.Host.Equals(TrustedReleaseHost, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new ReleaseInfo(version, url.AbsoluteUri);
    }
}

using System.Text.Json;
using DTech.Core.Updates;
using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Updates;

internal sealed class GitHubReleaseSource(HttpClient client, IOptions<UpdateOptions> options, ILogger<GitHubReleaseSource> logger) : IReleaseSource
{
    public async Task<ReleaseLookup> GetLatestRelease(CancellationToken cancellationToken)
    {
        var path = $"repos/{options.Value.Repository}/releases?per_page=30";
        try
        {
            using var response = await client.GetAsync(path, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug($"Release lookup returned status {(int)response.StatusCode}");
                return ReleaseLookup.Failed;
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            var releases = JsonSerializer.Deserialize<List<GitHubReleaseDto>>(payload) ?? [];
            var latest = GitHubReleaseSelector.SelectLatest(releases);
            return latest is null ? ReleaseLookup.NoRelease : ReleaseLookup.Found(latest);
        }
        catch (Exception exception) when (IsExpectedFailure(exception, cancellationToken))
        {
            logger.LogDebug($"Release lookup failed: {exception.GetType().Name}");
            return ReleaseLookup.Failed;
        }
    }

    private static bool IsExpectedFailure(Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException)
        {
            return !cancellationToken.IsCancellationRequested;
        }

        return exception is HttpRequestException or JsonException;
    }
}

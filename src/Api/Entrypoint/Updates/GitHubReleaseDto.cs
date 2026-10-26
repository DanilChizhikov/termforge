using System.Text.Json.Serialization;

namespace DTech.Entrypoint.Updates;

internal sealed record GitHubReleaseDto(
    [property: JsonPropertyName("tag_name")] string? TagName,
    [property: JsonPropertyName("html_url")] string? HtmlUrl,
    [property: JsonPropertyName("draft")] bool Draft,
    [property: JsonPropertyName("prerelease")] bool Prerelease);

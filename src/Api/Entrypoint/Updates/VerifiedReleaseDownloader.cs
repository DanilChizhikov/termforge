using System.Security.Cryptography;
using System.Text.Json;
using DTech.Core.Updates;

namespace DTech.Entrypoint.Updates;

public sealed class VerifiedReleaseDownloader(HttpClient client)
{
    private const string Repository = "DanilChizhikov/termforge";
    private const string ManifestName = "release-manifest.json";
    private static readonly HashSet<string> RuntimeIdentifiers = ["linux-x64", "linux-arm64", "win-x64", "win-arm64", "osx-x64", "osx-arm64"];

    public async Task<VerifiedReleasePackage> DownloadAsync(ReleasePackageRequest request, CancellationToken cancellationToken)
    {
        var release = request.Release;
        if (release.Version.IsPreRelease || !RuntimeIdentifiers.Contains(request.RuntimeIdentifier))
            throw new InvalidDataException("Only stable releases for supported runtime identifiers are accepted.");
        var prefix = $"https://github.com/{Repository}/releases/tag/";
        if (!release.Url.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidDataException("The release must belong to the official repository.");
        var tag = release.Url[prefix.Length..];
        if (!SemanticVersion.TryParse(tag, out var tagVersion) || tagVersion!.CompareTo(release.Version) != 0)
            throw new InvalidDataException("The release tag does not match the requested version.");
        var metadataUrl = $"https://api.github.com/repos/{Repository}/releases/tags/{Uri.EscapeDataString(tag)}";
        using var metadata = await ReadJsonAsync(metadataUrl, cancellationToken);
        var root = metadata.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean() || root.GetProperty("tag_name").GetString() != tag)
            throw new InvalidDataException("The release is not a published stable release.");
        var manifestUrl = FindAsset(root, tag, ManifestName);
        using var manifest = await ReadJsonAsync(manifestUrl, cancellationToken);
        var document = manifest.RootElement;
        var version = release.Version.ToString();
        if (document.GetProperty("schemaVersion").GetInt32() != 1 || document.GetProperty("version").GetString() != version)
            throw new InvalidDataException("The manifest schema or version does not match.");
        var extension = request.RuntimeIdentifier.StartsWith("win-", StringComparison.Ordinal) ? "zip" : "tar.gz";
        var fileName = $"termforge-{version}-{request.RuntimeIdentifier}.{extension}";
        var packages = document.GetProperty("packages").EnumerateArray().Where(candidate => candidate.GetProperty("rid").GetString() == request.RuntimeIdentifier).ToArray();
        if (packages.Length != 1) throw new InvalidDataException("The manifest must contain exactly one matching package.");
        var package = packages[0];
        var size = package.GetProperty("size").GetInt64();
        var sha256 = package.GetProperty("sha256").GetString();
        if (package.GetProperty("fileName").GetString() != fileName || size <= 0 || sha256 is null || sha256.Length != 64 || !sha256.All(Uri.IsHexDigit))
            throw new InvalidDataException("The package metadata is invalid.");
        var assetUrl = FindAsset(root, tag, fileName);
        var download = new PackageDownload(assetUrl, size, sha256);
        Directory.CreateDirectory(request.DestinationDirectory);
        var destination = Path.Combine(request.DestinationDirectory, $"{Guid.NewGuid():N}-{fileName}");
        var temporary = $"{destination}.partial";
        try
        {
            await DownloadVerifiedAsync(download, temporary, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination);
            return new VerifiedReleasePackage(destination, version, request.RuntimeIdentifier);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private async Task DownloadVerifiedAsync(PackageDownload download, string path, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(download.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is { } length && length != download.Size)
            throw new InvalidDataException("The package size does not match the manifest.");
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[65536];
        long received = 0;
        int count;
        while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            received += count;
            if (received > download.Size) throw new InvalidDataException("The package exceeds its declared size.");
            hash.AppendData(buffer, 0, count);
            var chunk = buffer.AsMemory(0, count);
            await output.WriteAsync(chunk, cancellationToken);
        }
        var actual = hash.GetHashAndReset();
        var expected = Convert.FromHexString(download.Sha256);
        if (received != download.Size || !CryptographicOperations.FixedTimeEquals(actual, expected))
            throw new InvalidDataException("Package integrity verification failed.");
    }

    private async Task<JsonDocument> ReadJsonAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (output.Length + count > 1024 * 1024) throw new InvalidDataException("Release metadata exceeds its size limit.");
            output.Write(buffer, 0, count);
        }
        var bytes = output.ToArray();
        return JsonDocument.Parse(bytes);
    }

    private static string FindAsset(JsonElement release, string tag, string name)
    {
        var matches = release.GetProperty("assets").EnumerateArray().Where(asset => asset.GetProperty("name").GetString() == name).ToArray();
        if (matches.Length != 1) throw new InvalidDataException($"Expected exactly one release asset named {name}.");
        var url = matches[0].GetProperty("browser_download_url").GetString();
        var expected = $"https://github.com/{Repository}/releases/download/{Uri.EscapeDataString(tag)}/{name}";
        if (url != expected) throw new InvalidDataException("The asset URL does not belong to this official release.");
        return expected;
    }

    private sealed record PackageDownload(string Url, long Size, string Sha256);
}

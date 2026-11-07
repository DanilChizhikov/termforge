using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DTech.Core.Updates;
using DTech.Entrypoint.Updates;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Updates;

[TestClass]
public sealed class VerifiedReleaseDownloaderTests
{
    private string _directory = null!;
    private const string Archive = "termforge-1.3.0-osx-arm64.tar.gz";
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("verified package contents");

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        var name = $"termforge-package-test-{Guid.NewGuid():N}";
        var temporary = Path.GetTempPath();
        _directory = Path.Combine(temporary, name);
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_directory, true);

    [TestMethod]
    public async Task Downloads_only_after_integrity_verification()
    {
        using var handler = new PackageHandler();
        using var client = new HttpClient(handler);
        var downloader = new VerifiedReleaseDownloader(client);
        var request = Request();

        var result = await downloader.DownloadAsync(request, TestContext.CancellationToken);

        var contents = await File.ReadAllBytesAsync(result.Path, TestContext.CancellationToken);
        contents.Should().Equal(Payload);
        result.RuntimeIdentifier.Should().Be("osx-arm64");
        Directory.GetFiles(_directory, "*.partial").Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("hash")]
    [DataRow("size")]
    [DataRow("version")]
    [DataRow("rid")]
    [DataRow("missing")]
    [DataRow("malformed")]
    [DataRow("prerelease")]
    [DataRow("draft")]
    [DataRow("asset")]
    [DataRow("duplicate")]
    [DataRow("schema")]
    public async Task Rejects_invalid_packages_without_leaving_files(string fault)
    {
        using var handler = new PackageHandler { Fault = fault };
        using var client = new HttpClient(handler);
        var downloader = new VerifiedReleaseDownloader(client);
        var request = Request();

        var act = () => downloader.DownloadAsync(request, TestContext.CancellationToken);

        await act.Should().ThrowAsync<Exception>();
        Directory.GetFiles(_directory).Should().BeEmpty();
    }

    [TestMethod]
    public async Task Rejects_another_repository_before_network_access()
    {
        using var handler = new PackageHandler();
        using var client = new HttpClient(handler);
        var downloader = new VerifiedReleaseDownloader(client);
        SemanticVersion.TryParse("1.3.0", out var version);
        var release = new ReleaseInfo(version!, "https://github.com/another/repository/releases/tag/1.3.0");
        var request = new ReleasePackageRequest(release, "osx-arm64", _directory);

        var act = () => downloader.DownloadAsync(request, TestContext.CancellationToken);

        await act.Should().ThrowAsync<InvalidDataException>();
        handler.Requests.Should().Be(0);
    }

    [TestMethod]
    public async Task Cancellation_during_download_removes_partial_file()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new PackageHandler { CancelDownload = cancellation };
        using var client = new HttpClient(handler);
        var downloader = new VerifiedReleaseDownloader(client);
        var request = Request();

        var act = () => downloader.DownloadAsync(request, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        Directory.GetFiles(_directory).Should().BeEmpty();
    }

    private ReleasePackageRequest Request()
    {
        SemanticVersion.TryParse("1.3.0", out var version);
        var release = new ReleaseInfo(version!, "https://github.com/DanilChizhikov/termforge/releases/tag/1.3.0");
        return new ReleasePackageRequest(release, "osx-arm64", _directory);
    }

    private sealed class PackageHandler : HttpMessageHandler
    {
        public string? Fault { get; init; }
        public CancellationTokenSource? CancelDownload { get; init; }
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            var url = request.RequestUri!.AbsoluteUri;
            HttpContent content;
            if (url.StartsWith("https://api.github.com/", StringComparison.Ordinal)) content = Metadata();
            else if (url.EndsWith("release-manifest.json", StringComparison.Ordinal)) content = Manifest();
            else
            {
                var bytes = Payload.ToArray();
                if (Fault == "hash") bytes[0] ^= 1;
                content = CancelDownload is null ? new ByteArrayContent(bytes) : new StreamContent(new CancelledStream(CancelDownload));
            }
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            return Task.FromResult(response);
        }

        private StringContent Metadata()
        {
            var names = Fault == "missing" ? new[] { Archive } : new[] { Archive, "release-manifest.json" };
            var assets = names.Select(name => new { name, browser_download_url = $"https://github.com/{(Fault == "asset" ? "other/repo" : "DanilChizhikov/termforge")}/releases/download/1.3.0/{name}" });
            var value = new { tag_name = "1.3.0", draft = Fault == "draft", prerelease = Fault == "prerelease", assets };
            var json = JsonSerializer.Serialize(value);
            return new StringContent(json);
        }

        private StringContent Manifest()
        {
            var hash = SHA256.HashData(Payload);
            var package = new { rid = Fault == "rid" ? "win-x64" : "osx-arm64", fileName = Archive, size = Payload.Length + (Fault == "size" ? 1 : 0), sha256 = Convert.ToHexStringLower(hash) };
            var packages = Fault == "duplicate" ? new[] { package, package } : [package];
            var value = new { schemaVersion = Fault == "schema" ? 2 : 1, version = Fault == "version" ? "1.2.0" : "1.3.0", packages };
            var json = Fault == "malformed" ? "invalid" : JsonSerializer.Serialize(value);
            return new StringContent(json);
        }
    }

    private sealed class CancelledStream(CancellationTokenSource cancellation) : MemoryStream(Payload)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await cancellation.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }
}

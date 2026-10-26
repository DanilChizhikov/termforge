using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Files;

[TestClass]
public sealed class FileSearchEndpointsTests
{
    private const string LocalSearchPrefix = "/api/files/local/search";

    private TermForgeApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private AuthenticatedSession _session = null!;
    private string _workDirectory = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _factory = new TermForgeApplicationFactory();
        _client = _factory.CreateClientWithoutCookies();
        _session = await SessionBootstrapper.AuthenticateAsync(_factory, _client, TestContext.CancellationToken);
        _workDirectory = Path.Combine(Path.GetTempPath(), $"termforge-search-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workDirectory);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        if (Directory.Exists(_workDirectory))
        {
            Directory.Delete(_workDirectory, true);
        }
    }

    [TestMethod]
    public async Task LocalSearch_WithoutSession_IsUnauthorized()
    {
        var path = SearchPath(_workDirectory, "x");

        using var response = await TestRequest.Get(path).SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task RemoteSearch_WithoutSession_IsUnauthorized()
    {
        var path = $"/api/files/remote/{Guid.NewGuid()}/search?path=%2F&pattern=x";

        using var response = await TestRequest.Get(path).SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task LocalSearch_StreamsMatchesAsNdjsonAndEndsWithSummary()
    {
        var nested = Path.Combine(_workDirectory, "nested");
        Directory.CreateDirectory(nested);
        await File.WriteAllTextAsync(Path.Combine(_workDirectory, "app.log"), "a", TestContext.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(nested, "error.log"), "b", TestContext.CancellationToken);

        using var response = await Get(SearchPath(_workDirectory, "*.log"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/x-ndjson");
        var records = await ReadRecordsAsync(response);
        var matches = records.Where(record => record.GetProperty("type").GetString() == "match").ToList();
        matches.Select(match => match.GetProperty("entry").GetProperty("name").GetString()).Should().BeEquivalentTo("app.log", "error.log");
        matches.Should().Contain(match => match.GetProperty("parentPath").GetString() == nested);
        var completed = records.Last();
        completed.GetProperty("type").GetString().Should().Be("completed");
        var summary = completed.GetProperty("summary");
        summary.GetProperty("outcome").GetString().Should().Be("Completed");
        summary.GetProperty("method").GetString().Should().Be("Local");
        summary.GetProperty("matchCount").GetInt32().Should().Be(2);
        summary.GetProperty("isPartial").GetBoolean().Should().BeFalse();
    }

    [TestMethod]
    public async Task LocalSearch_ResultLimit_IsReportedAsPartial()
    {
        for (var i = 0; i < 3; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(_workDirectory, $"file{i}.log"), "x", TestContext.CancellationToken);
        }

        var path = $"{SearchPath(_workDirectory, "*.log")}&maxResults=1";

        using var response = await Get(path);

        var records = await ReadRecordsAsync(response);
        records.Count(record => record.GetProperty("type").GetString() == "match").Should().Be(1);
        var summary = records.Last().GetProperty("summary");
        summary.GetProperty("outcome").GetString().Should().Be("ResultLimitReached");
        summary.GetProperty("isPartial").GetBoolean().Should().BeTrue();
    }

    [TestMethod]
    public async Task LocalSearch_EmptyPattern_IsBadRequestWithCode()
    {
        using var response = await Get(SearchPath(_workDirectory, " "));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertErrorCode(response, "Invalid");
    }

    [TestMethod]
    public async Task LocalSearch_OutOfRangeDepth_IsBadRequest()
    {
        var path = $"{SearchPath(_workDirectory, "x")}&maxDepth=1000";

        using var response = await Get(path);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task LocalSearch_MissingRoot_IsNotFound()
    {
        var missing = Path.Combine(_workDirectory, "missing");

        using var response = await Get(SearchPath(missing, "x"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertErrorCode(response, "NotFound");
    }

    [TestMethod]
    public async Task RemoteSearch_UnknownConnection_IsNotFound()
    {
        var path = $"/api/files/remote/{Guid.NewGuid()}/search?path=%2Fsrv&pattern=x";

        using var response = await Get(path);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static string SearchPath(string root, string pattern)
    {
        var encodedRoot = Uri.EscapeDataString(root);
        var encodedPattern = Uri.EscapeDataString(pattern);
        return $"{LocalSearchPrefix}?path={encodedRoot}&pattern={encodedPattern}";
    }

    private Task<HttpResponseMessage> Get(string path)
    {
        return TestRequest.Get(path)
            .WithSession(_session)
            .SendAsync(_client, TestContext.CancellationToken);
    }

    private async Task<List<JsonElement>> ReadRecordsAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var records = new List<JsonElement>(lines.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            var record = JsonSerializer.Deserialize<JsonElement>(lines[i]);
            records.Add(record);
        }

        return records;
    }

    private async Task AssertErrorCode(HttpResponseMessage response, string expectedCode)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        body.GetProperty("code").GetString().Should().Be(expectedCode);
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Files;

[TestClass]
public sealed class FileBrowsingEndpointsTests
{
    private const string LocalPrefix = "/api/files/local";

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
        _workDirectory = Path.Combine(Path.GetTempPath(), $"termforge-files-tests-{Guid.NewGuid():N}");
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
    public async Task LocalList_WithoutSession_IsUnauthorized()
    {
        var path = $"{LocalPrefix}/list?path={Uri.EscapeDataString(_workDirectory)}";

        using var response = await TestRequest.Get(path).SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task RemoteList_WithoutSession_IsUnauthorized()
    {
        var path = $"/api/files/remote/{Guid.NewGuid()}/list";

        using var response = await TestRequest.Get(path).SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task LocalRoots_ReturnsRootsList()
    {
        using var response = await Get($"{LocalPrefix}/roots");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var roots = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        roots.GetArrayLength().Should().BeGreaterThan(0);
    }

    [TestMethod]
    public async Task LocalList_ReturnsEntriesWithKindAndSize()
    {
        await File.WriteAllTextAsync(Path.Combine(_workDirectory, "a.txt"), "abc", TestContext.CancellationToken);
        Directory.CreateDirectory(Path.Combine(_workDirectory, "folder"));

        using var response = await Get(ListPath(_workDirectory));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        body.GetProperty("path").GetString().Should().Be(_workDirectory);
        var entries = body.GetProperty("entries").EnumerateArray().ToList();
        entries.Should().Contain(entry => entry.GetProperty("name").GetString() == "a.txt" && entry.GetProperty("kind").GetString() == "File" && entry.GetProperty("size").GetInt64() == 3);
        entries.Should().Contain(entry => entry.GetProperty("name").GetString() == "folder" && entry.GetProperty("kind").GetString() == "Directory");
    }

    [TestMethod]
    public async Task LocalList_TraversalPath_IsBadRequestWithCode()
    {
        var traversal = $"{_workDirectory}{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}other";

        using var response = await Get(ListPath(traversal));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertErrorCode(response, "Invalid");
    }

    [TestMethod]
    public async Task LocalList_MissingDirectory_IsNotFoundWithCode()
    {
        var missing = Path.Combine(_workDirectory, "missing");

        using var response = await Get(ListPath(missing));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertErrorCode(response, "NotFound");
    }

    [TestMethod]
    public async Task LocalCreateDirectory_CreatesDirectory()
    {
        var body = new { parentPath = _workDirectory, name = "made" };

        using var response = await Post($"{LocalPrefix}/directories", body);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Directory.Exists(Path.Combine(_workDirectory, "made")).Should().BeTrue();
    }

    [TestMethod]
    public async Task LocalCreateDirectory_WithoutCsrfToken_IsRejected()
    {
        var body = new { parentPath = _workDirectory, name = "made" };

        using var response = await TestRequest.Post($"{LocalPrefix}/directories")
            .WithSession(_session)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithJson(body)
            .SendAsync(_client, TestContext.CancellationToken);

        response.IsSuccessStatusCode.Should().BeFalse();
        Directory.Exists(Path.Combine(_workDirectory, "made")).Should().BeFalse();
    }

    [TestMethod]
    public async Task LocalRename_RenamesEntry()
    {
        var source = Path.Combine(_workDirectory, "old.txt");
        await File.WriteAllTextAsync(source, "x", TestContext.CancellationToken);
        var body = new { path = source, newName = "new.txt" };

        using var response = await Post($"{LocalPrefix}/rename", body);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        File.Exists(Path.Combine(_workDirectory, "new.txt")).Should().BeTrue();
    }

    [TestMethod]
    public async Task LocalRename_ToExistingName_IsConflict()
    {
        var source = Path.Combine(_workDirectory, "a.txt");
        await File.WriteAllTextAsync(source, "a", TestContext.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_workDirectory, "b.txt"), "b", TestContext.CancellationToken);
        var body = new { path = source, newName = "b.txt" };

        using var response = await Post($"{LocalPrefix}/rename", body);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertErrorCode(response, "AlreadyExists");
    }

    [TestMethod]
    public async Task LocalDelete_WithoutConfirmation_IsPreconditionRequiredAndKeepsFile()
    {
        var file = Path.Combine(_workDirectory, "keep.txt");
        await File.WriteAllTextAsync(file, "x", TestContext.CancellationToken);
        var body = new { path = file };

        using var response = await Post($"{LocalPrefix}/delete", body);

        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
        await AssertErrorCode(response, "ConfirmationRequired");
        File.Exists(file).Should().BeTrue();
    }

    [TestMethod]
    public async Task LocalDelete_WithExplicitFalseConfirmation_KeepsFile()
    {
        var file = Path.Combine(_workDirectory, "keep.txt");
        await File.WriteAllTextAsync(file, "x", TestContext.CancellationToken);
        var body = new { path = file, confirm = false };

        using var response = await Post($"{LocalPrefix}/delete", body);

        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
        File.Exists(file).Should().BeTrue();
    }

    [TestMethod]
    public async Task LocalDelete_WithConfirmation_RemovesFile()
    {
        var file = Path.Combine(_workDirectory, "gone.txt");
        await File.WriteAllTextAsync(file, "x", TestContext.CancellationToken);
        var body = new { path = file, confirm = true };

        using var response = await Post($"{LocalPrefix}/delete", body);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        File.Exists(file).Should().BeFalse();
    }

    [TestMethod]
    public async Task RemoteList_UnknownConnection_IsNotFound()
    {
        var path = $"/api/files/remote/{Guid.NewGuid()}/list";

        using var response = await Get(path);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertErrorCode(response, "NotFound");
    }

    [TestMethod]
    public async Task RemoteDelete_WithoutConfirmation_IsPreconditionRequired()
    {
        var path = $"/api/files/remote/{Guid.NewGuid()}/delete";
        var body = new { path = "/home/user/file" };

        using var response = await Post(path, body);

        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
        await AssertErrorCode(response, "ConfirmationRequired");
    }

    [TestMethod]
    public async Task RemoteList_TraversalPath_IsBadRequest()
    {
        var path = $"/api/files/remote/{Guid.NewGuid()}/list?path={Uri.EscapeDataString("/home/../etc")}";

        using var response = await Get(path);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static string ListPath(string directory)
    {
        return $"{LocalPrefix}/list?path={Uri.EscapeDataString(directory)}";
    }

    private Task<HttpResponseMessage> Get(string path)
    {
        return TestRequest.Get(path)
            .WithSession(_session)
            .SendAsync(_client, TestContext.CancellationToken);
    }

    private Task<HttpResponseMessage> Post<TBody>(string path, TBody body)
    {
        return TestRequest.Post(path)
            .WithSession(_session)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithCsrfToken(_session.CsrfToken)
            .WithJson(body)
            .SendAsync(_client, TestContext.CancellationToken);
    }

    private async Task AssertErrorCode(HttpResponseMessage response, string expectedCode)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        body.GetProperty("code").GetString().Should().Be(expectedCode);
    }
}

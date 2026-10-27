using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DTech.Core.Connections;
using DTech.Core.Editor;
using DTech.Core.Files;
using DTech.Core.Safety;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace DTech.Entrypoint.Tests.Editor;

[TestClass]
public sealed class EditorEndpointsTests
{
    private const string FilePath = "/etc/app/app.yml";
    private const string Content = "name: app\n";
    private const int FileMode = 0b110_100_100;

    private static readonly Guid ConnectionId = Guid.NewGuid();
    private static readonly DateTime ModifiedUtc = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

    private IRemoteEditorFileClient _fileClient = null!;
    private TermForgeApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private AuthenticatedSession _session = null!;

    public TestContext TestContext { get; set; } = null!;

    private static string FileRoute => $"/api/editor/remote/{ConnectionId}/file";

    [TestInitialize]
    public async Task Initialize()
    {
        _fileClient = Substitute.For<IRemoteEditorFileClient>();
        StubExistingFile(Content);
        _factory = new TermForgeApplicationFactory { ConfigureServices = ReplaceServices };
        _client = _factory.CreateClientWithoutCookies();
        _session = await SessionBootstrapper.AuthenticateAsync(_factory, _client, TestContext.CancellationToken);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [TestMethod]
    public async Task Open_WithoutSession_IsUnauthorized()
    {
        var path = OpenPath(FilePath);

        var request = TestRequest.Get(path);
        using var response = await request.SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Open_ReturnsContentVersionAndPermissions()
    {
        var path = OpenPath(FilePath);
        using var response = await Send(HttpMethod.Get, path, null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        body.GetProperty("path").GetString().Should().Be(FilePath);
        body.GetProperty("content").GetString().Should().Be(Content);
        body.GetProperty("permissions").GetString().Should().Be("rw-r--r--");
        var version = body.GetProperty("version");
        version.GetProperty("size").GetInt64().Should().Be(Content.Length);
        version.GetProperty("sha256").GetString().Should().HaveLength(64);
    }

    [TestMethod]
    public async Task Open_BinaryFile_IsUnsupportedMediaTypeWithCode()
    {
        byte[] binary = [0x00, 0xFF, 0x10];
        StubExistingBytes(binary);

        var path = OpenPath(FilePath);
        using var response = await Send(HttpMethod.Get, path, null);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        await AssertErrorCode(response, "Binary");
    }

    [TestMethod]
    public async Task Open_TraversalPath_IsBadRequest()
    {
        var path = OpenPath("/etc/../root/.ssh/id_rsa");
        using var response = await Send(HttpMethod.Get, path, null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertErrorCode(response, "Invalid");
    }

    [TestMethod]
    public async Task Save_WithoutCsrfToken_IsRejected()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, FileRoute);
        request.Headers.Add("Origin", TermForgeApplicationFactory.ApplicationOrigin);
        request.Headers.Add("Cookie", _session.CookieHeader);
        var body = new { path = FilePath, content = "x", hasByteOrderMark = false };
        request.Content = JsonContent.Create(body);

        using var response = await _client.SendAsync(request, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task Save_StaleVersion_IsConflictWithCurrentContent()
    {
        var opened = await OpenAsync();
        var version = opened.GetProperty("version");
        StubExistingFile("name: changed\n");
        var body = new { path = FilePath, content = "name: mine\n", hasByteOrderMark = false, expectedVersion = version };

        using var response = await Send(HttpMethod.Put, FileRoute, body);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var conflict = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        conflict.GetProperty("code").GetString().Should().Be("Conflict");
        conflict.GetProperty("currentContent").GetString().Should().Be("name: changed\n");
        conflict.GetProperty("operationId").GetGuid().Should().NotBeEmpty();
        await _fileClient.DidNotReceive().CreateFileAsync(Arg.Any<RemoteFileCreation>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Save_MatchingVersion_ReturnsNewVersionAndBackup()
    {
        var opened = await OpenAsync();
        var version = opened.GetProperty("version");
        StubSuccessfulWrites();
        var body = new { path = FilePath, content = "name: mine\n", hasByteOrderMark = false, expectedVersion = version };

        using var response = await Send(HttpMethod.Put, FileRoute, body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        saved.GetProperty("strategy").GetString().Should().Be("AtomicRename");
        saved.GetProperty("backupName").GetString().Should().StartWith(".app.yml.termforge-");
        saved.GetProperty("version").GetProperty("size").GetInt64().Should().Be("name: mine\n".Length);
    }

    [TestMethod]
    public async Task Rollback_UnknownBackupName_IsBadRequest()
    {
        var confirmations = _factory.Services.GetRequiredService<IConfirmationTokenStore>();
        var scope = new ConfirmationScope(DangerousOperation.FileRollback, ConnectionId, FilePath);
        var grant = confirmations.Issue(scope);
        var body = new { path = FilePath, backupName = "../../etc/shadow", confirmationToken = grant.Token };

        var path = $"/api/editor/remote/{ConnectionId}/rollback";
        using var response = await Send(HttpMethod.Post, path, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertErrorCode(response, "Invalid");
    }

    [TestMethod]
    public async Task Rollback_WithoutConfirmationToken_IsPreconditionRequired()
    {
        var body = new { path = FilePath, backupName = "app.env.bak" };

        var path = $"/api/editor/remote/{ConnectionId}/rollback";
        using var response = await Send(HttpMethod.Post, path, body);

        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
        await AssertErrorCode(response, "ConfirmationRequired");
    }

    [TestMethod]
    public async Task Open_UnknownConnection_IsNotFound()
    {
        var unknown = Guid.NewGuid();
        var escaped = Uri.EscapeDataString(FilePath);
        var path = $"/api/editor/remote/{unknown}/file?path={escaped}";

        using var response = await Send(HttpMethod.Get, path, null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertErrorCode(response, "NotFound");
    }

    private void ReplaceServices(IServiceCollection services)
    {
        services.RemoveAll<IRemoteEditorConnectionOpener>();
        var opener = Substitute.For<IRemoteEditorConnectionOpener>();
        opener.OpenAsync(ConnectionId, Arg.Any<CancellationToken>()).Returns(_ => OpenSession());
        opener.OpenAsync(Arg.Is<Guid>(id => id != ConnectionId), Arg.Any<CancellationToken>()).Returns(_ => MissingConnection());
        services.AddSingleton(opener);
    }

    private EditorResult<RemoteEditorSession> OpenSession()
    {
        var session = new RemoteEditorSession(_fileClient, "app-server");
        return EditorResult<RemoteEditorSession>.Success(session);
    }

    private static EditorResult<RemoteEditorSession> MissingConnection()
    {
        var missing = new EditorError(EditorErrorCode.NotFound, "Saved connection was not found.");
        return EditorResult<RemoteEditorSession>.Failure(missing);
    }

    private void StubExistingFile(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        StubExistingBytes(bytes);
    }

    private void StubExistingBytes(byte[] bytes)
    {
        var modifiedUtc = ModifiedUtc.AddTicks(bytes.Length);
        var attributes = new RemoteFileAttributes(RemoteFileKind.File, bytes.Length, modifiedUtc) { Mode = FileMode };
        var attributesResult = SshResult<RemoteFileAttributes>.Success(attributes);
        var readResult = SshResult<byte[]>.Success(bytes);
        _fileClient.GetAttributesAsync(FilePath, Arg.Any<CancellationToken>()).Returns(attributesResult);
        _fileClient.ReadAsync(Arg.Is<EditorReadRequest>(request => request.Path == FilePath), Arg.Any<CancellationToken>()).Returns(readResult);
    }

    private void StubSuccessfulWrites()
    {
        var created = new RemoteFileAttributes(RemoteFileKind.File, 0, ModifiedUtc) { Mode = FileMode };
        var createdResult = SshResult<RemoteFileAttributes>.Success(created);
        var replacedResult = SshResult<AtomicReplaceOutcome>.Success(AtomicReplaceOutcome.Replaced);
        IReadOnlyList<string> names = [];
        var namesResult = SshResult<IReadOnlyList<string>>.Success(names);
        _fileClient.CreateFileAsync(Arg.Any<RemoteFileCreation>(), Arg.Any<CancellationToken>()).Returns(createdResult);
        _fileClient.ReplaceAtomicallyAsync(Arg.Any<RemoteRenameRequest>(), Arg.Any<CancellationToken>()).Returns(replacedResult);
        _fileClient.ListNamesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(namesResult);
    }

    private async Task<JsonElement> OpenAsync()
    {
        var path = OpenPath(FilePath);
        using var response = await Send(HttpMethod.Get, path, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
    }

    private static string OpenPath(string path)
    {
        var escaped = Uri.EscapeDataString(path);
        return $"{FileRoute}?path={escaped}";
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Origin", TermForgeApplicationFactory.ApplicationOrigin);
        request.Headers.Add("Cookie", _session.CookieHeader);
        request.Headers.Add("X-CSRF-Token", _session.CsrfToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request, TestContext.CancellationToken);
    }

    private async Task AssertErrorCode(HttpResponseMessage response, string expectedCode)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        body.GetProperty("code").GetString().Should().Be(expectedCode);
    }
}

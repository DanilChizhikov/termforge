using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DTech.Core.Connections;
using DTech.Core.Credentials;
using DTech.Core.Editor;
using DTech.Core.Editor.Sudo;
using DTech.Core.Files;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace DTech.Entrypoint.Tests.Editor;

[TestClass]
public sealed class SudoEditorEndpointsTests
{
    private const string FilePath = "/etc/nginx/nginx.conf";
    private const string Content = "worker_processes 1;\n";
    private const string Password = "hunter2-sudo";
    private const int FileMode = 0b110_000_000;
    private const int OwnerId = 33;

    private static readonly Guid ConnectionId = Guid.NewGuid();
    private static readonly DateTime ModifiedUtc = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

    private IRemoteEditorFileClient _fileClient = null!;
    private ISudoEditorSessionOpener _opener = null!;
    private ISudoStatusReader _statuses = null!;
    private IFileAccessReader _accessReader = null!;
    private ICredentialStore _credentialStore = null!;
    private TermForgeApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private AuthenticatedSession _session = null!;

    public TestContext TestContext { get; set; } = null!;

    private static string RoutePrefix => $"/api/editor/remote/{ConnectionId}/sudo";

    [TestInitialize]
    public async Task Initialize()
    {
        _fileClient = Substitute.For<IRemoteEditorFileClient>();
        StubExistingFile();
        _opener = Substitute.For<ISudoEditorSessionOpener>();
        _opener.OpenAsync(ConnectionId, Arg.Any<SudoCredentials>(), Arg.Any<CancellationToken>()).Returns(_ => OpenSession());
        _statuses = Substitute.For<ISudoStatusReader>();
        _accessReader = Substitute.For<IFileAccessReader>();
        _credentialStore = Substitute.For<ICredentialStore>();
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
    public async Task Status_ReturnsAvailabilityAndSavedPasswordFlag()
    {
        var status = new SudoStatus(SudoAvailability.PasswordRequired, true, SudoErrors.PasswordRequiredMessage);
        var statusResult = EditorResult<SudoStatus>.Success(status);
        _statuses.ReadAsync(ConnectionId, Arg.Any<CancellationToken>()).Returns(statusResult);

        using var response = await Send(HttpMethod.Get, $"{RoutePrefix}/status", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        body.GetProperty("availability").GetString().Should().Be("PasswordRequired");
        body.GetProperty("hasSavedPassword").GetBoolean().Should().BeTrue();
    }

    [TestMethod]
    public async Task Access_ReturnsOwnerNameAndWritability()
    {
        FileAccessRequest? received = null;
        var access = new RemoteFileAccess(OwnerId, "www-data", false);
        var accessResult = EditorResult<RemoteFileAccess>.Success(access);
        _accessReader.ReadAsync(Arg.Do<FileAccessRequest>(request => received = request), Arg.Any<CancellationToken>()).Returns(accessResult);
        var escapedPath = Uri.EscapeDataString(FilePath);

        using var response = await Send(HttpMethod.Get, $"{RoutePrefix}/access?path={escapedPath}&ownerId={OwnerId}", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        body.GetProperty("ownerId").GetInt32().Should().Be(OwnerId);
        body.GetProperty("ownerName").GetString().Should().Be("www-data");
        body.GetProperty("writable").GetBoolean().Should().BeFalse();
        var expected = new FileAccessRequest(ConnectionId, FilePath, OwnerId);
        received.Should().Be(expected);
    }

    [TestMethod]
    public async Task Access_WithoutOwnerId_IsBadRequest()
    {
        var escapedPath = Uri.EscapeDataString(FilePath);

        using var response = await Send(HttpMethod.Get, $"{RoutePrefix}/access?path={escapedPath}", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Open_PassesPasswordToOpenerAndNeverReturnsOrLogsIt()
    {
        SudoCredentials? received = null;
        _opener.OpenAsync(ConnectionId, Arg.Do<SudoCredentials>(credentials => received = credentials), Arg.Any<CancellationToken>()).Returns(_ => OpenSession());
        var body = new { path = FilePath, sudo = new { password = Password, rememberPassword = true } };

        using var response = await Send(HttpMethod.Post, $"{RoutePrefix}/open", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var text = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        text.Should().NotContain(Password);
        var document = JsonDocument.Parse(text).RootElement;
        document.GetProperty("content").GetString().Should().Be(Content);
        document.GetProperty("permissions").GetString().Should().Be("rw-------");
        document.GetProperty("ownerId").GetInt32().Should().Be(OwnerId);
        received!.Password.Should().Be(Password);
        received.RememberPassword.Should().BeTrue();
        _factory.Logs.Messages.Should().NotContain(message => message.Contains(Password, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Open_WithoutSudoBody_UsesNoCredentials()
    {
        SudoCredentials? received = null;
        _opener.OpenAsync(ConnectionId, Arg.Do<SudoCredentials>(credentials => received = credentials), Arg.Any<CancellationToken>()).Returns(_ => OpenSession());
        var body = new { path = FilePath };

        using var response = await Send(HttpMethod.Post, $"{RoutePrefix}/open", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        received!.Password.Should().BeNull();
        received.RememberPassword.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(EditorErrorCode.SudoPasswordIncorrect)]
    [DataRow(EditorErrorCode.SudoPasswordRequired)]
    [DataRow(EditorErrorCode.SudoUnavailable)]
    public async Task Save_SudoRefused_IsUnprocessableWithCode(EditorErrorCode code)
    {
        var refused = new EditorError(code, "sudo refused");
        var failure = EditorResult<RemoteEditorSession>.Failure(refused);
        _opener.OpenAsync(ConnectionId, Arg.Any<SudoCredentials>(), Arg.Any<CancellationToken>()).Returns(failure);
        var body = new { path = FilePath, content = "x", hasByteOrderMark = false, expectedVersion = (object?)null, sudo = new { password = Password } };

        using var response = await Send(HttpMethod.Put, $"{RoutePrefix}/file", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        problem.GetProperty("code").GetString().Should().Be(code.ToString());
    }

    [TestMethod]
    public async Task Save_WithoutCsrfToken_IsForbidden()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{RoutePrefix}/file");
        request.Headers.Add("Origin", TermForgeApplicationFactory.ApplicationOrigin);
        request.Headers.Add("Cookie", _session.CookieHeader);
        var body = new { path = FilePath, content = "x", sudo = new { password = Password } };
        request.Content = JsonContent.Create(body);

        using var response = await _client.SendAsync(request, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _opener.DidNotReceive().OpenAsync(Arg.Any<Guid>(), Arg.Any<SudoCredentials>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ForgetPassword_DeletesSudoCredential()
    {
        var deleted = CredentialResult.Success();
        _credentialStore.Delete(Arg.Any<CredentialKey>(), Arg.Any<CancellationToken>()).Returns(deleted);

        using var response = await Send(HttpMethod.Delete, $"{RoutePrefix}/password", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var key = SudoPasswordStore.KeyFor(ConnectionId);
        await _credentialStore.Received(1).Delete(key, Arg.Any<CancellationToken>());
    }

    private void ReplaceServices(IServiceCollection services)
    {
        services.RemoveAll<ISudoEditorSessionOpener>();
        services.RemoveAll<ISudoStatusReader>();
        services.RemoveAll<IFileAccessReader>();
        services.RemoveAll<ICredentialStore>();
        services.AddSingleton(_opener);
        services.AddSingleton(_statuses);
        services.AddSingleton(_accessReader);
        services.AddSingleton(_credentialStore);
    }

    private EditorResult<RemoteEditorSession> OpenSession()
    {
        var session = new RemoteEditorSession(_fileClient, "web-01");
        return EditorResult<RemoteEditorSession>.Success(session);
    }

    private void StubExistingFile()
    {
        var bytes = Encoding.UTF8.GetBytes(Content);
        var attributes = new RemoteFileAttributes(RemoteFileKind.File, bytes.Length, ModifiedUtc) { Mode = FileMode, UserId = OwnerId };
        var attributesResult = SshResult<RemoteFileAttributes>.Success(attributes);
        var readResult = SshResult<byte[]>.Success(bytes);
        _fileClient.GetAttributesAsync(FilePath, Arg.Any<CancellationToken>()).Returns(attributesResult);
        _fileClient.ReadAsync(Arg.Is<EditorReadRequest>(request => request.Path == FilePath), Arg.Any<CancellationToken>()).Returns(readResult);
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
}

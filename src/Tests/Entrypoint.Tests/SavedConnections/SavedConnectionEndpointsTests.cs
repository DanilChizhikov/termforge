using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DTech.Core.Connections;
using DTech.Core.Credentials;
using DTech.Core.SavedConnections;
using DTech.Core.Terminal;
using DTech.Entrypoint.SavedConnections;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace DTech.Entrypoint.Tests.SavedConnections;

[TestClass]
public sealed class SavedConnectionEndpointsTests
{
    private const string Password = "endpoint-secret-9";
    private const string ListPath = "/api/connections";

    private readonly Dictionary<string, string> _storedSecrets = [];
    private string _configDirectory = null!;
    private string _configPath = null!;
    private ITerminalSessionFactory _sessionFactory = null!;
    private TermForgeApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private AuthenticatedSession _session = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _configDirectory = Path.Combine(Path.GetTempPath(), $"termforge-sshconfig-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_configDirectory);
        _configPath = Path.Combine(_configDirectory, "config");
        _sessionFactory = Substitute.For<ITerminalSessionFactory>();
        _factory = new TermForgeApplicationFactory { ConfigureServices = ReplaceServices };
        _client = _factory.CreateClientWithoutCookies();
        _session = await SessionBootstrapper.AuthenticateAsync(_factory, _client, TestContext.CancellationToken);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        Directory.Delete(_configDirectory, true);
    }

    [TestMethod]
    public async Task Create_ListUpdateAndDelete_WorkEndToEnd()
    {
        using var created = await SendJson(HttpMethod.Post, ListPath, Request("web"));
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        var id = createdBody.GetProperty("id").GetGuid();

        using var listed = await SendJson(HttpMethod.Get, ListPath, null);
        var listBody = await listed.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        using var updated = await SendJson(HttpMethod.Put, $"{ListPath}/{id}", Request("web", "prod"));
        var updatedBody = await updated.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        using var deleted = await SendJson(HttpMethod.Delete, $"{ListPath}/{id}", null);
        using var afterDelete = await SendJson(HttpMethod.Get, $"{ListPath}/{id}", null);

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        listBody.GetArrayLength().Should().Be(1);
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        updatedBody.GetProperty("groupName").GetString().Should().Be("prod");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        afterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Create_WithPassword_StoresItOnlyInCredentialStoreAndNeverEchoesIt()
    {
        var request = Request("db");
        request.AuthMethod = "Password";
        request.Password = Password;

        using var response = await SendJson(HttpMethod.Post, ListPath, request);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var listed = await SendJson(HttpMethod.Get, ListPath, null);
        var listBody = await listed.Content.ReadAsStringAsync(TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        body.Should().NotContain(Password);
        listBody.Should().NotContain(Password);
        body.Should().Contain("\"hasPassword\":true");
        _storedSecrets.Values.Should().ContainSingle().Which.Should().Be(Password);
        ReadAllStoredValues().Should().NotContain(Password);
    }

    [TestMethod]
    public async Task Create_WithInvalidPayload_ReturnsBadRequestProblem()
    {
        var request = Request("bad");
        request.Port = 0;

        using var response = await SendJson(HttpMethod.Post, ListPath, request);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("Invalid");
    }

    [TestMethod]
    public async Task Create_WithDuplicateName_ReturnsConflict()
    {
        using var first = await SendJson(HttpMethod.Post, ListPath, Request("web"));

        using var second = await SendJson(HttpMethod.Post, ListPath, Request("WEB"));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task Requests_WithoutSession_AreRejected()
    {
        using var response = await TestRequest.Get(ListPath).SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Create_WithoutCsrfToken_IsForbidden()
    {
        using var response = await TestRequest.Post(ListPath)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithSession(_session)
            .WithJson(Request("web"))
            .SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task Test_WhenTransportConnects_ReturnsConnectedWithLatency()
    {
        var id = await CreateAsync("web");
        var session = Substitute.For<ITerminalSession>();
        _sessionFactory.OpenAsync(default!, default).ReturnsForAnyArgs(SshResult<ITerminalSession>.Success(session));

        using var response = await SendJson(HttpMethod.Post, $"{ListPath}/{id}/test", null);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        using var fetched = await SendJson(HttpMethod.Get, $"{ListPath}/{id}", null);
        var fetchedBody = await fetched.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("state").GetString().Should().Be("Connected");
        body.GetProperty("latencyMilliseconds").GetInt64().Should().BeGreaterThanOrEqualTo(0);
        fetchedBody.GetProperty("lastConnectedAt").ValueKind.Should().Be(JsonValueKind.String);
    }

    [TestMethod]
    public async Task Test_WhenAuthenticationFails_ReturnsActionableStateWithoutSecrets()
    {
        var request = Request("db");
        request.AuthMethod = "Password";
        request.Password = Password;
        using var created = await SendJson(HttpMethod.Post, ListPath, request);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken)).GetProperty("id").GetGuid();
        var error = new SshError(SshErrorKind.AuthenticationFailed, "Connecting failed");
        _sessionFactory.OpenAsync(default!, default).ReturnsForAnyArgs(SshResult<ITerminalSession>.Failure(error));

        using var response = await SendJson(HttpMethod.Post, $"{ListPath}/{id}/test", null);
        var text = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        text.Should().Contain("AuthenticationFailed").And.NotContain(Password);
    }

    [TestMethod]
    public async Task Test_OfUnknownConnection_ReturnsNotFound()
    {
        using var response = await SendJson(HttpMethod.Post, $"{ListPath}/{Guid.NewGuid()}/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task ImportSshConfig_CreatesConnectionsAndReportsUnsupportedDirectives()
    {
        await File.WriteAllTextAsync(_configPath, "Host web\n    HostName 10.0.0.5\n    Port 2200\n    ForwardAgent yes\n", TestContext.CancellationToken);

        using var response = await SendJson(HttpMethod.Post, $"{ListPath}/import/ssh-config", null);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        using var listed = await SendJson(HttpMethod.Get, ListPath, null);
        var listBody = await listed.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("created").GetInt32().Should().Be(1);
        body.GetProperty("issues")[0].GetProperty("directive").GetString().Should().Be("ForwardAgent");
        listBody[0].GetProperty("port").GetInt32().Should().Be(2200);
        listBody[0].GetProperty("sshAlias").GetString().Should().Be("web");
    }

    [TestMethod]
    public async Task ImportSshConfig_WithoutConfigFile_ReturnsNotFound()
    {
        using var response = await SendJson(HttpMethod.Post, $"{ListPath}/import/ssh-config", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Groups_CanBeRenamedAndRemoved()
    {
        var request = Request("web", "old");
        using var created = await SendJson(HttpMethod.Post, ListPath, request);

        var rename = new RenameGroupRequest { NewName = "new" };
        using var renamed = await SendJson(HttpMethod.Put, "/api/connection-groups/old", rename);
        using var afterRename = await SendJson(HttpMethod.Get, ListPath, null);
        var renamedBody = await afterRename.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        using var removed = await SendJson(HttpMethod.Delete, "/api/connection-groups/new", null);
        using var afterRemove = await SendJson(HttpMethod.Get, ListPath, null);
        var removedBody = await afterRemove.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);

        renamed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        renamedBody[0].GetProperty("groupName").GetString().Should().Be("new");
        removed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        removedBody[0].GetProperty("groupName").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [TestMethod]
    public void SavedConnectionRequest_ToString_IsRedacted()
    {
        var request = Request("web");
        request.Password = Password;

        request.ToString().Should().NotContain(Password).And.Contain(Secret.Redacted);
    }

    private static SavedConnectionRequest Request(string name, string? group = null)
    {
        return new SavedConnectionRequest
        {
            Name = name,
            Host = "example.org",
            Port = 22,
            UserName = "deploy",
            AuthMethod = "Agent",
            GroupName = group,
        };
    }

    private void ReplaceServices(IServiceCollection services)
    {
        var credentialStore = Substitute.For<ICredentialStore>();
        credentialStore.Store(default, default!, default).ReturnsForAnyArgs(call =>
        {
            _storedSecrets[call.Arg<CredentialKey>().Value] = call.Arg<Secret>().RevealString();
            return Task.FromResult(CredentialResult.Success());
        });
        credentialStore.Read(default, default).ReturnsForAnyArgs(call => ReadSecret(call.Arg<CredentialKey>()));
        credentialStore.Delete(default, default).ReturnsForAnyArgs(call =>
        {
            _storedSecrets.Remove(call.Arg<CredentialKey>().Value);
            return Task.FromResult(CredentialResult.Success());
        });
        services.RemoveAll<ICredentialStore>();
        services.AddSingleton(credentialStore);
        services.RemoveAll<ITerminalSessionFactory>();
        services.AddSingleton(_sessionFactory);
        services.RemoveAll<SshConfigImportOptions>();
        services.AddSingleton(new SshConfigImportOptions(_configPath));
    }

    private Task<CredentialResult<Secret>> ReadSecret(CredentialKey key)
    {
        if (!_storedSecrets.TryGetValue(key.Value, out var value))
        {
            return Task.FromResult(CredentialResult<Secret>.Failure(CredentialErrors.NotFound(key)));
        }

        var secret = Secret.FromString(value);
        return Task.FromResult(CredentialResult<Secret>.Success(secret));
    }

    private string FindDatabasePath()
    {
        var options = _factory.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        return options["Persistence:DatabasePath"]!;
    }

    private string ReadAllStoredValues()
    {
        var builder = new System.Text.StringBuilder();
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={FindDatabasePath()};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM saved_connections";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            for (var i = 0; i < reader.FieldCount; i++)
            {
                builder.Append(reader.GetValue(i)).Append('|');
            }
        }

        return builder.ToString();
    }

    private async Task<Guid> CreateAsync(string name)
    {
        using var created = await SendJson(HttpMethod.Post, ListPath, Request(name));
        var body = await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> SendJson(HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Origin", TermForgeApplicationFactory.ApplicationOrigin);
        request.Headers.Add("Cookie", _session.CookieHeader);
        request.Headers.Add("X-CSRF-Token", _session.CsrfToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        return _client.SendAsync(request, TestContext.CancellationToken);
    }
}

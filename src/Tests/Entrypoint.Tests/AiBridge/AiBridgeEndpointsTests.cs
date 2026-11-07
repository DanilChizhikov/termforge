using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DTech.Core.AiBridge.Settings;
using DTech.Core.Diagnostics;
using DTech.Core.Operations;
using DTech.Entrypoint.Tests.Docker.Support;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace DTech.Entrypoint.Tests.AiBridge;

[TestClass]
public sealed class AiBridgeEndpointsTests
{
    private const string McpPath = "/mcp";
    private const string ToolsListRequest = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}";

    private ScriptedDockerRunner _runner = null!;
    private TermForgeApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private AuthenticatedSession _session = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _runner = new ScriptedDockerRunner();
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
    public async Task Settings_WithoutSession_IsUnauthorized()
    {
        using var anonymous = _factory.CreateClientWithoutCookies();

        using var response = await TestRequest.Get("/api/ai-bridge/settings").SendAsync(anonymous, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Settings_AreOffByDefaultAndRequireApproval()
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/ai-bridge/settings", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("enabled").GetBoolean().Should().BeFalse();
        body.GetProperty("requireApproval").GetBoolean().Should().BeTrue();
        body.GetProperty("hasToken").GetBoolean().Should().BeFalse();
        body.GetProperty("mcpUrl").GetString().Should().Be("http://localhost/mcp");
    }

    [TestMethod]
    public async Task Mcp_WhenBridgeDisabled_IsNotFound()
    {
        var token = await CreateTokenAsync();

        using var response = await PostMcpAsync(token, null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Mcp_WithoutToken_IsUnauthorized()
    {
        await CreateTokenAsync();
        await UpdateSettingsAsync(true, true);

        using var response = await PostMcpAsync(null, null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().Should().Be("Bearer");
    }

    [TestMethod]
    public async Task Mcp_WithRevokedToken_IsUnauthorized()
    {
        var oldToken = await CreateTokenAsync();
        using var listed = await SendAsync(HttpMethod.Get, "/api/ai-bridge/tokens", null);
        var tokens = await ReadJsonAsync(listed);
        var id = tokens[0].GetProperty("id").GetGuid();
        using var revoked = await SendAsync(HttpMethod.Delete, $"/api/ai-bridge/tokens/{id}", null);
        revoked.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await UpdateSettingsAsync(true, true);

        using var response = await PostMcpAsync(oldToken, null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Mcp_FromAnotherOrigin_IsForbidden()
    {
        var token = await CreateTokenAsync();
        await UpdateSettingsAsync(true, true);

        using var response = await PostMcpAsync(token, "http://evil.example");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task Mcp_WithToken_ListsTheBridgeTools()
    {
        var token = await CreateTokenAsync();
        await UpdateSettingsAsync(true, true);
        await using var mcp = await ConnectMcpAsync(token);

        var tools = await mcp.ListToolsAsync(cancellationToken: TestContext.CancellationToken);

        tools.Select(tool => tool.Name).Should().BeEquivalentTo(
            "list_servers",
            "run_command",
            "list_terminals",
            "read_terminal_output",
            "send_to_terminal",
            "docker_list_containers",
            "docker_list_images",
            "docker_container_logs",
            "docker_container_action",
            "read_file",
            "write_file");
    }

    [TestMethod]
    public async Task ListServers_ReturnsOnlyServersWithAiAccess()
    {
        var token = await CreateTokenAsync();
        await UpdateSettingsAsync(true, true);
        var open = await CreateConnectionAsync("web", true);
        await CreateConnectionAsync("private", false);
        await using var mcp = await ConnectMcpAsync(token);

        var result = await mcp.CallToolAsync("list_servers", cancellationToken: TestContext.CancellationToken);

        result.IsError.Should().NotBe(true);
        var servers = ParseText(result);
        servers.GetArrayLength().Should().Be(1);
        servers[0].GetProperty("id").GetGuid().Should().Be(open);
    }

    [TestMethod]
    public async Task RunCommand_WithApprovalsOff_ReturnsTheOutput()
    {
        var token = await CreateTokenAsync();
        await UpdateSettingsAsync(true, false);
        var serverId = await CreateConnectionAsync("web", true);
        _runner.Respond("uptime", 0, "up 3 days\n");
        await using var mcp = await ConnectMcpAsync(token);

        var arguments = new Dictionary<string, object?> { ["serverId"] = serverId, ["command"] = "uptime" };
        var result = await mcp.CallToolAsync("run_command", arguments, cancellationToken: TestContext.CancellationToken);

        result.IsError.Should().NotBe(true);
        var body = ParseText(result);
        body.GetProperty("exitCode").GetInt32().Should().Be(0);
        body.GetProperty("stdout").GetString().Should().Be("up 3 days\n");
        _runner.ExecutedCommands.Should().Contain("uptime");
    }

    [TestMethod]
    public async Task RunCommand_WaitsForApprovalInTheBrowser()
    {
        var token = await CreateTokenAsync();
        await UpdateSettingsAsync(true, true);
        var serverId = await CreateConnectionAsync("web", true);
        _runner.Respond("uptime", 0, "up\n");
        await using var mcp = await ConnectMcpAsync(token);

        var arguments = new Dictionary<string, object?> { ["serverId"] = serverId, ["command"] = "uptime" };
        var call = mcp.CallToolAsync("run_command", arguments, cancellationToken: TestContext.CancellationToken).AsTask();
        var approval = await WaitForApprovalAsync();
        approval.GetProperty("kind").GetString().Should().Be("command");
        approval.GetProperty("summary").GetString().Should().Be("uptime");
        approval.GetProperty("serverName").GetString().Should().Be("web");
        _runner.ExecutedCommands.Should().BeEmpty();
        var id = approval.GetProperty("id").GetGuid();
        using var decision = await SendAsync(HttpMethod.Post, $"/api/ai-bridge/approvals/{id}", new { approve = true });
        var result = await call;

        decision.StatusCode.Should().Be(HttpStatusCode.NoContent);
        result.IsError.Should().NotBe(true);
        _runner.ExecutedCommands.Should().Contain("uptime");
    }

    [TestMethod]
    public async Task RunCommand_DeniedInTheBrowser_ReturnsAnError()
    {
        var token = await CreateTokenAsync();
        await UpdateSettingsAsync(true, true);
        var serverId = await CreateConnectionAsync("web", true);
        await using var mcp = await ConnectMcpAsync(token);

        var arguments = new Dictionary<string, object?> { ["serverId"] = serverId, ["command"] = "reboot" };
        var call = mcp.CallToolAsync("run_command", arguments, cancellationToken: TestContext.CancellationToken).AsTask();
        var approval = await WaitForApprovalAsync();
        var id = approval.GetProperty("id").GetGuid();
        using var decision = await SendAsync(HttpMethod.Post, $"/api/ai-bridge/approvals/{id}", new { approve = false });
        var result = await call;

        result.IsError.Should().BeTrue();
        TextOf(result).Should().Contain("denied");
        _runner.ExecutedCommands.Should().BeEmpty();
    }

    [TestMethod]
    public async Task RunCommand_OnServerWithoutAiAccess_ReturnsAnError()
    {
        var token = await CreateTokenAsync();
        await UpdateSettingsAsync(true, false);
        var serverId = await CreateConnectionAsync("private", false);
        await using var mcp = await ConnectMcpAsync(token);

        var arguments = new Dictionary<string, object?> { ["serverId"] = serverId, ["command"] = "uptime" };
        var result = await mcp.CallToolAsync("run_command", arguments, cancellationToken: TestContext.CancellationToken);

        result.IsError.Should().BeTrue();
        TextOf(result).Should().Contain("AI access is turned off");
        _runner.ExecutedCommands.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Decide_UnknownApproval_IsNotFound()
    {
        using var response = await SendAsync(HttpMethod.Post, $"/api/ai-bridge/approvals/{Guid.NewGuid()}", new { approve = true });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Connections_RoundTripTheAiAccessFlag()
    {
        var id = await CreateConnectionAsync("web", true);

        using var response = await SendAsync(HttpMethod.Get, $"/api/connections/{id}", null);

        var body = await ReadJsonAsync(response);
        body.GetProperty("aiAccess").GetBoolean().Should().BeTrue();
    }

    [TestMethod]
    public async Task Tokens_CreateListAndRevoke_AreIndependentAndNeverDiscloseHashes()
    {
        var first = await CreateNamedTokenAsync(" First ");
        var second = await CreateNamedTokenAsync("Second");
        var firstSecret = first.GetProperty("token").GetString()!;
        var secondSecret = second.GetProperty("token").GetString()!;
        first.GetProperty("name").GetString().Should().Be("First");
        await UpdateSettingsAsync(true, true);

        await using (var mcp = await ConnectMcpAsync(firstSecret))
        {
            var tools = await mcp.ListToolsAsync(cancellationToken: TestContext.CancellationToken);
            tools.Should().NotBeEmpty();
        }

        using var listed = await SendAsync(HttpMethod.Get, "/api/ai-bridge/tokens", null);
        var list = await ReadJsonAsync(listed);
        list.GetArrayLength().Should().Be(2);
        for (var i = 0; i < list.GetArrayLength(); i++)
        {
            list[i].EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo("id", "name", "createdAt");
        }

        var id = first.GetProperty("id").GetGuid();
        using var revoked = await SendAsync(HttpMethod.Delete, $"/api/ai-bridge/tokens/{id}", null);
        revoked.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var rejected = await PostMcpAsync(firstSecret, null);
        rejected.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await using var remaining = await ConnectMcpAsync(secondSecret);
        var remainingTools = await remaining.ListToolsAsync(cancellationToken: TestContext.CancellationToken);
        remainingTools.Should().NotBeEmpty();
        using var settings = await SendAsync(HttpMethod.Get, "/api/ai-bridge/settings", null);
        var settingsBody = await ReadJsonAsync(settings);
        settingsBody.GetProperty("hasToken").GetBoolean().Should().BeTrue();
        var secondId = second.GetProperty("id").GetGuid();
        using var lastRevoked = await SendAsync(HttpMethod.Delete, $"/api/ai-bridge/tokens/{secondId}", null);
        lastRevoked.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var emptySettings = await SendAsync(HttpMethod.Get, "/api/ai-bridge/settings", null);
        var emptySettingsBody = await ReadJsonAsync(emptySettings);
        emptySettingsBody.GetProperty("hasToken").GetBoolean().Should().BeFalse();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task Tokens_InvalidName_IsBadRequest(string? name)
    {
        var request = new { name };
        using var response = await SendAsync(HttpMethod.Post, "/api/ai-bridge/tokens", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Tokens_LongName_IsBadRequest()
    {
        var request = new { name = new string('x', 101) };
        using var response = await SendAsync(HttpMethod.Post, "/api/ai-bridge/tokens", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Tokens_DuplicateName_IsConflict()
    {
        await CreateNamedTokenAsync("Agent");
        var request = new { name = " agent " };
        using var response = await SendAsync(HttpMethod.Post, "/api/ai-bridge/tokens", request);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task Tokens_UnknownId_IsNotFound()
    {
        var id = Guid.NewGuid();
        using var response = await SendAsync(HttpMethod.Delete, $"/api/ai-bridge/tokens/{id}", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestMethod]
    [DataRow("GET")]
    [DataRow("POST")]
    [DataRow("DELETE")]
    public async Task Tokens_WithoutSession_IsUnauthorized(string verb)
    {
        var path = verb == "DELETE" ? "/api/ai-bridge/tokens/00000000-0000-0000-0000-000000000001" : "/api/ai-bridge/tokens";
        var method = new HttpMethod(verb);
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Origin", TermForgeApplicationFactory.ApplicationOrigin);
        var body = new { name = "Agent" };
        request.Content = JsonContent.Create(body);

        using var response = await _client.SendAsync(request, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    [DataRow("POST", false)]
    [DataRow("DELETE", false)]
    [DataRow("POST", true)]
    [DataRow("DELETE", true)]
    public async Task Tokens_WithoutCsrfOrWithForeignOrigin_IsForbidden(string verb, bool foreignOrigin)
    {
        var path = verb == "DELETE" ? "/api/ai-bridge/tokens/00000000-0000-0000-0000-000000000001" : "/api/ai-bridge/tokens";
        var method = new HttpMethod(verb);
        using var request = new HttpRequestMessage(method, path);
        var origin = foreignOrigin ? "http://evil.example" : TermForgeApplicationFactory.ApplicationOrigin;
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Cookie", _session.CookieHeader);
        if (foreignOrigin) request.Headers.Add("X-CSRF-Token", _session.CsrfToken);
        var body = new { name = "Agent" };
        request.Content = JsonContent.Create(body);

        using var response = await _client.SendAsync(request, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task Revocation_DoesNotCancelAnAlreadyPendingApproval()
    {
        var created = await CreateNamedTokenAsync("Agent");
        var token = created.GetProperty("token").GetString()!;
        var tokenId = created.GetProperty("id").GetGuid();
        await UpdateSettingsAsync(true, true);
        var server = await CreateConnectionAsync("web", true);
        _runner.Respond("uptime", 0, "up\n");
        await using var mcp = await ConnectMcpAsync(token);
        var arguments = new Dictionary<string, object?> { ["serverId"] = server, ["command"] = "uptime" };
        var call = mcp.CallToolAsync("run_command", arguments, cancellationToken: TestContext.CancellationToken).AsTask();
        var approval = await WaitForApprovalAsync();

        using var revoked = await SendAsync(HttpMethod.Delete, $"/api/ai-bridge/tokens/{tokenId}", null);
        revoked.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var approvalId = approval.GetProperty("id").GetGuid();
        var decision = new { approve = true };
        using var approved = await SendAsync(HttpMethod.Post, $"/api/ai-bridge/approvals/{approvalId}", decision);
        approved.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var result = await call;

        result.IsError.Should().NotBe(true);
        _runner.ExecutedCommands.Should().Contain("uptime");
        using var rejected = await PostMcpAsync(token, null);
        rejected.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task TokenLifecycle_DoesNotWriteSecretsOrHashesToLogsOrJournal()
    {
        var created = await CreateNamedTokenAsync("Agent");
        var token = created.GetProperty("token").GetString()!;
        var hash = AiBridgeTokenHasher.Hash(token);
        var id = created.GetProperty("id").GetGuid();
        await UpdateSettingsAsync(true, false);
        await using (var mcp = await ConnectMcpAsync(token))
        {
            await mcp.ListToolsAsync(cancellationToken: TestContext.CancellationToken);
        }

        var wrong = $"invalid-{token}";
        using var invalid = await PostMcpAsync(wrong, null);
        invalid.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var duplicate = new { name = "agent" };
        using var conflict = await SendAsync(HttpMethod.Post, "/api/ai-bridge/tokens", duplicate);
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var revoked = await SendAsync(HttpMethod.Delete, $"/api/ai-bridge/tokens/{id}", null);
        revoked.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var rejected = await PostMcpAsync(token, null);
        rejected.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var logs = string.Join('\n', _factory.Logs.Messages);
        logs.Should().NotContain(token).And.NotContain(hash).And.NotContain(wrong);
        using var scope = _factory.Services.CreateScope();
        var operations = scope.ServiceProvider.GetRequiredService<IOperationRepository>();
        var query = OperationQuery.Unfiltered;
        var journal = await operations.Query(query, TestContext.CancellationToken);
        var journalJson = JsonSerializer.Serialize(journal);
        journalJson.Should().NotContain(token).And.NotContain(hash).And.NotContain(wrong);
    }

    private async Task<JsonElement> CreateNamedTokenAsync(string name)
    {
        var request = new { name };
        using var response = await SendAsync(HttpMethod.Post, "/api/ai-bridge/tokens", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        return await ReadJsonAsync(response);
    }

    private void ReplaceServices(IServiceCollection services)
    {
        services.RemoveAll<IRemoteCommandRunnerFactory>();
        services.AddSingleton<IRemoteCommandRunnerFactory>(_runner);
    }

    private async Task<McpClient> ConnectMcpAsync(string token)
    {
        var httpClient = _factory.CreateClientWithoutCookies();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var endpoint = new Uri(httpClient.BaseAddress!, McpPath);
        var options = new HttpClientTransportOptions { Endpoint = endpoint, TransportMode = HttpTransportMode.StreamableHttp };
        var transport = new HttpClientTransport(options, httpClient, null, true);
        return await McpClient.CreateAsync(transport, null, null, TestContext.CancellationToken);
    }

    private async Task<HttpResponseMessage> PostMcpAsync(string? token, string? origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, McpPath);
        request.Content = new StringContent(ToolsListRequest, Encoding.UTF8, "application/json");
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        return await _client.SendAsync(request, TestContext.CancellationToken);
    }

    private async Task<JsonElement> WaitForApprovalAsync()
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            using var response = await SendAsync(HttpMethod.Get, "/api/ai-bridge/approvals", null);
            var body = await ReadJsonAsync(response);
            if (body.GetArrayLength() > 0)
            {
                return body[0].Clone();
            }

            await Task.Delay(50, TestContext.CancellationToken);
        }

        throw new AssertFailedException("No approval request appeared.");
    }

    private async Task<string> CreateTokenAsync()
    {
        var name = Guid.NewGuid().ToString();
        var request = new { name };
        using var response = await SendAsync(HttpMethod.Post, "/api/ai-bridge/tokens", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(response);
        return body.GetProperty("token").GetString()!;
    }

    private async Task UpdateSettingsAsync(bool enabled, bool requireApproval)
    {
        using var response = await SendAsync(HttpMethod.Put, "/api/ai-bridge/settings", new { enabled, requireApproval });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<Guid> CreateConnectionAsync(string name, bool aiAccess)
    {
        var request = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["host"] = $"{name}.example.test",
            ["port"] = 22,
            ["userName"] = "deploy",
            ["authMethod"] = "Agent",
            ["aiAccess"] = aiAccess,
        };
        using var response = await SendAsync(HttpMethod.Post, "/api/connections", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(response);
        return body.GetProperty("id").GetGuid();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body)
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

    private async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
    }

    private static string TextOf(CallToolResult result)
    {
        return result.Content.OfType<TextContentBlock>().Single().Text;
    }

    private static JsonElement ParseText(CallToolResult result)
    {
        using var document = JsonDocument.Parse(TextOf(result));
        return document.RootElement.Clone();
    }
}

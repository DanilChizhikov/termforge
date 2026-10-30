using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DTech.Core.Diagnostics;
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
        var token = await RegenerateTokenAsync();

        using var response = await PostMcpAsync(token, null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Mcp_WithoutToken_IsUnauthorized()
    {
        await RegenerateTokenAsync();
        await UpdateSettingsAsync(true, true);

        using var response = await PostMcpAsync(null, null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().Should().Be("Bearer");
    }

    [TestMethod]
    public async Task Mcp_WithReplacedToken_IsUnauthorized()
    {
        var oldToken = await RegenerateTokenAsync();
        await RegenerateTokenAsync();
        await UpdateSettingsAsync(true, true);

        using var response = await PostMcpAsync(oldToken, null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Mcp_FromAnotherOrigin_IsForbidden()
    {
        var token = await RegenerateTokenAsync();
        await UpdateSettingsAsync(true, true);

        using var response = await PostMcpAsync(token, "http://evil.example");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task Mcp_WithToken_ListsTheBridgeTools()
    {
        var token = await RegenerateTokenAsync();
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
        var token = await RegenerateTokenAsync();
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
        var token = await RegenerateTokenAsync();
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
        var token = await RegenerateTokenAsync();
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
        var token = await RegenerateTokenAsync();
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
        var token = await RegenerateTokenAsync();
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

    private async Task<string> RegenerateTokenAsync()
    {
        using var response = await SendAsync(HttpMethod.Post, "/api/ai-bridge/token", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
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

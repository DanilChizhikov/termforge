using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DTech.Core.Files;
using DTech.Core.Operations;
using DTech.Core.Terminal;
using DTech.Core.Transfers;
using DTech.Core.Workspace;
using DTech.Entrypoint.Tests.Infrastructure;
using DTech.Entrypoint.Tests.Terminals.Support;
using DTech.Entrypoint.Tests.Transfers;
using DTech.Entrypoint.Transfers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace DTech.Entrypoint.Tests.Lifecycle.Support;

internal sealed class LifecycleApiHarness : IAsyncDisposable
{
    public const string ExitPath = "/api/lifecycle/exit";
    public const string AttentionPath = "/api/lifecycle/attention";

    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(15);

    private readonly TestContext _testContext;
    private readonly string _workDirectory = Path.Combine(Path.GetTempPath(), $"termforge-lifecycle-tests-{Guid.NewGuid():N}");
    private HttpClient _client = null!;
    private RecordingTransferJournal? _journal;
    private AuthenticatedSession _session = null!;

    private LifecycleApiHarness(TestContext testContext, IReadOnlyDictionary<string, string?> settings, Action<IServiceCollection>? configureServices)
    {
        _testContext = testContext;
        Factory = new TermForgeApplicationFactory(settings)
        {
            ConfigureServices = services =>
            {
                ReplaceRemoteAccess(services);
                configureServices?.Invoke(services);
            },
        };
    }

    public TermForgeApplicationFactory Factory { get; }

    public ScriptedTerminalSessionFactory TerminalSessions { get; } = new();

    public TaskCompletionSource TransferGate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string WorkDirectory => _workDirectory;

    public IReadOnlyList<OperationStatus> JournaledTransferOutcomes => _journal?.JournaledOutcomes ?? [];

    public static async Task<LifecycleApiHarness> StartAsync(TestContext testContext, IReadOnlyDictionary<string, string?>? settings = null, Action<IServiceCollection>? configureServices = null)
    {
        var effectiveSettings = settings ?? new Dictionary<string, string?>();
        var harness = new LifecycleApiHarness(testContext, effectiveSettings, configureServices);
        Directory.CreateDirectory(harness._workDirectory);
        harness._client = harness.Factory.CreateClientWithoutCookies();
        harness._session = await SessionBootstrapper.AuthenticateAsync(harness.Factory, harness._client, testContext.CancellationToken);
        return harness;
    }

    public async ValueTask DisposeAsync()
    {
        TransferGate.TrySetResult();
        _client.Dispose();
        await Factory.DisposeAsync();
        if (Directory.Exists(_workDirectory))
        {
            Directory.Delete(_workDirectory, true);
        }
    }

    public async Task<Guid> CreateConnectionAsync(string name)
    {
        var body = new { name, host = $"{name}.example.test", port = 22, userName = "deploy", authMethod = "Agent" };
        using var response = await SendAsync(HttpMethod.Post, "/api/connections", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync(response);
        return created.GetProperty("id").GetGuid();
    }

    public async Task OpenTerminalAsync(Guid connectionId)
    {
        var body = new { connectionId, columns = 80, rows = 24 };
        using var response = await SendAsync(HttpMethod.Post, "/api/terminals", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    public async Task<string> StartDownloadAsync(Guid connectionId, string remotePath)
    {
        var body = new { connectionId, direction = "download", sourcePaths = new[] { remotePath }, targetDirectory = _workDirectory, overwrite = false };
        using var response = await SendAsync(HttpMethod.Post, "/api/transfers", body);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var transfers = await ReadJsonAsync(response);
        var id = transfers[0].GetProperty("id").GetString()!;
        await WaitForTransferStatusAsync("running");
        return id;
    }

    public async Task SaveDraftAsync(Guid connectionId, string path)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IWorkspaceStateStore>();
        var document = WorkspaceStateSerializer.Serialize(WorkspaceState.Empty);
        var draft = new WorkspaceEditorDraft(connectionId, path, "listen 80;", null);
        WorkspaceEditorDraft[] drafts = [draft];
        await store.SaveAsync(document, drafts, _testContext.CancellationToken);
    }

    public async Task<JsonElement> GetAttentionAsync()
    {
        using var response = await SendAsync(HttpMethod.Get, AttentionPath, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadJsonAsync(response);
    }

    public Task<HttpResponseMessage> RequestExitAsync(bool waitForTransfers)
    {
        var body = new { waitForTransfers };
        return SendAsync(HttpMethod.Post, ExitPath, body);
    }

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Origin", TermForgeApplicationFactory.ApplicationOrigin);
        request.Headers.Add("Cookie", _session.CookieHeader);
        request.Headers.Add("X-CSRF-Token", _session.CsrfToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request, _testContext.CancellationToken);
    }

    public async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<JsonElement>(_testContext.CancellationToken);
    }

    public async Task WaitForStoppingAsync()
    {
        var stopping = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = Factory.ApplicationStopping.Register(stopping.SetResult);
        await stopping.Task.WaitAsync(WaitTimeout, _testContext.CancellationToken);
    }

    private async Task WaitForTransferStatusAsync(string status)
    {
        var deadline = DateTime.UtcNow + WaitTimeout;
        while (DateTime.UtcNow < deadline)
        {
            using var response = await SendAsync(HttpMethod.Get, "/api/transfers", null);
            var list = await ReadJsonAsync(response);
            if (list.GetArrayLength() > 0 && list[0].GetProperty("status").GetString() == status)
            {
                return;
            }

            await Task.Delay(20, _testContext.CancellationToken);
        }

        Assert.Fail($"The transfer did not reach status {status}.");
    }

    private void ReplaceRemoteAccess(IServiceCollection services)
    {
        services.RemoveAll<ITerminalSessionFactory>();
        services.AddSingleton<ITerminalSessionFactory>(TerminalSessions);
        var opener = Substitute.For<ITransferConnectionOpener>();
        opener.OpenAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_ => CreateGatedClient());
        services.RemoveAll<ITransferConnectionOpener>();
        services.AddSingleton(opener);
        services.RemoveAll<ITransferJournal>();
        services.AddSingleton<ITransferJournal>(CreateRecordingJournal);
    }

    private RecordingTransferJournal CreateRecordingJournal(IServiceProvider provider)
    {
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var inner = new ScopedTransferJournal(scopeFactory);
        _journal = new RecordingTransferJournal(inner);
        return _journal;
    }

    private FileResult<IRemoteFileClient> CreateGatedClient()
    {
        var client = new GatedRemoteFileClient(TransferGate);
        return FileResult<IRemoteFileClient>.Success(client);
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DTech.Core.Files;
using DTech.Core.SavedConnections;
using DTech.Core.Transfers;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace DTech.Entrypoint.Tests.Transfers;

[TestClass]
public sealed class TransferEndpointsTests
{
    private const string TransfersPath = "/api/transfers";

    private static readonly Guid ConnectionId = Guid.NewGuid();

    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TermForgeApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private AuthenticatedSession _session = null!;
    private string _workDirectory = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        var settings = new Dictionary<string, string?> { ["Transfers:MaxParallelTransfers"] = "1" };
        _factory = new TermForgeApplicationFactory(settings) { ConfigureServices = ReplaceRemoteAccess };
        _client = _factory.CreateClientWithoutCookies();
        _session = await SessionBootstrapper.AuthenticateAsync(_factory, _client, TestContext.CancellationToken);
        _workDirectory = Path.Combine(Path.GetTempPath(), $"termforge-transfer-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workDirectory);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        _gate.TrySetResult();
        _client.Dispose();
        await _factory.DisposeAsync();
        if (Directory.Exists(_workDirectory))
        {
            Directory.Delete(_workDirectory, true);
        }
    }

    [TestMethod]
    public async Task List_WithoutSession_IsUnauthorized()
    {
        using var response = await TestRequest.Get(TransfersPath).SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Create_WithoutCsrfToken_IsRejected()
    {
        var body = CreateBody(["/remote/a.txt"], false);

        using var response = await TestRequest.Post(TransfersPath)
            .WithSession(_session)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithJson(body)
            .SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task Create_WithUnknownDirection_IsBadRequest()
    {
        var body = new { connectionId = ConnectionId, direction = "sideways", sourcePaths = new[] { "/remote/a.txt" }, targetDirectory = _workDirectory };

        using var response = await Post(TransfersPath, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Create_ExistingLocalTarget_ReturnsConflictUntilOverwriteConfirmed()
    {
        var existing = Path.Combine(_workDirectory, "a.txt");
        await File.WriteAllTextAsync(existing, "old", TestContext.CancellationToken);

        using var refused = await Post(TransfersPath, CreateBody(["/remote/a.txt"], false));
        using var accepted = await Post(TransfersPath, CreateBody(["/remote/a.txt"], true));

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        problem.GetProperty("code").GetString().Should().Be("TransferConflict");
        problem.GetProperty("conflicts")[0].GetString().Should().Be(existing);
        accepted.StatusCode.Should().Be(HttpStatusCode.Accepted);
        _gate.SetResult();
        await WaitForStatus(0, "succeeded");
        var content = await File.ReadAllBytesAsync(existing, TestContext.CancellationToken);
        content.Should().Equal(GatedRemoteFileClient.Content);
    }

    [TestMethod]
    public async Task Create_WithSingleWorker_RunsOneAtATimeAndQueuedCanBeCancelled()
    {
        using var created = await Post(TransfersPath, CreateBody(["/remote/first.txt", "/remote/second.txt"], false));
        created.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var transfers = await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        var secondId = transfers[1].GetProperty("id").GetString();

        await WaitForStatus(0, "running");
        var list = await ListAsync();
        list[1].GetProperty("status").GetString().Should().Be("queued");
        using var cancelled = await Post<object?>($"{TransfersPath}/{secondId}/cancel", null);
        cancelled.StatusCode.Should().Be(HttpStatusCode.Accepted);
        await WaitForStatus(1, "cancelled");
        _gate.SetResult();
        await WaitForStatus(0, "succeeded");

        File.Exists(Path.Combine(_workDirectory, "first.txt")).Should().BeTrue();
        File.Exists(Path.Combine(_workDirectory, "second.txt")).Should().BeFalse();
        Directory.GetFiles(_workDirectory, "*.part").Should().BeEmpty();
        using var operations = await Get("/api/operations?kind=transfer.download");
        var page = await operations.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        page.GetProperty("total").GetInt32().Should().Be(2);
    }

    [TestMethod]
    public async Task Cancel_RunningTransfer_RemovesPartialFile()
    {
        using var created = await Post(TransfersPath, CreateBody(["/remote/big.bin"], false));
        var transfers = await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        var id = transfers[0].GetProperty("id").GetString();
        await WaitForStatus(0, "running");

        using var cancelled = await Post<object?>($"{TransfersPath}/{id}/cancel", null);
        await WaitForStatus(0, "cancelled");

        cancelled.StatusCode.Should().Be(HttpStatusCode.Accepted);
        Directory.EnumerateFileSystemEntries(_workDirectory).Should().BeEmpty();
    }

    [TestMethod]
    public async Task Cancel_UnknownTransfer_IsNotFound()
    {
        using var response = await Post<object?>($"{TransfersPath}/{Guid.NewGuid()}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private void ReplaceRemoteAccess(IServiceCollection services)
    {
        var connection = new SavedConnection
        {
            Id = ConnectionId,
            Details = new SavedConnectionDraft { Name = "web-01", Host = "203.0.113.10", UserName = "deploy" },
        };
        var repository = Substitute.For<ISavedConnectionRepository>();
        repository.FindAsync(ConnectionId, Arg.Any<CancellationToken>()).Returns(connection);
        services.RemoveAll<ISavedConnectionRepository>();
        services.AddSingleton(repository);
        var opener = Substitute.For<ITransferConnectionOpener>();
        opener.OpenAsync(ConnectionId, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            var client = new GatedRemoteFileClient(_gate);
            return FileResult<IRemoteFileClient>.Success(client);
        });
        services.RemoveAll<ITransferConnectionOpener>();
        services.AddSingleton(opener);
    }

    private object CreateBody(string[] sources, bool overwrite)
    {
        return new { connectionId = ConnectionId, direction = "download", sourcePaths = sources, targetDirectory = _workDirectory, overwrite };
    }

    private async Task WaitForStatus(int index, string status)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            var list = await ListAsync();
            if (list.GetArrayLength() > index && list[index].GetProperty("status").GetString() == status)
            {
                return;
            }

            await Task.Delay(20, TestContext.CancellationToken);
        }

        Assert.Fail($"Transfer {index} did not reach status {status}.");
    }

    private async Task<JsonElement> ListAsync()
    {
        using var response = await Get(TransfersPath);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
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
}

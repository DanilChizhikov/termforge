using DTech.Core.AiBridge;
using DTech.Core.AiBridge.Approvals;
using DTech.Core.AiBridge.Settings;
using DTech.Core.Docker;
using DTech.Core.Operations;
using DTech.Core.SavedConnections;
using DTech.Core.Tests.Docker.Support;

namespace DTech.Core.Tests.AiBridge.Support;

internal sealed class AiBridgeTestGraph
{
    public AiBridgeTestGraph()
    {
        Docker = new DockerTestGraph();
        SettingsStore = new InMemoryAiBridgeSettingsStore { Stored = new AiBridgeSettings(true, true, null) };
        Settings = new AiBridgeSettingsService(SettingsStore, new SequentialTokenGenerator());
        Notifier = new ScriptedApprovalNotifier { Answer = AiApprovalOutcome.Approved };
        Broker = new AiApprovalBroker(new AiApprovalTimeout(TimeSpan.FromSeconds(5)), Notifier, TimeProvider.System);
        Notifier.Broker = Broker;
        Gate = new AiActionGate(Settings, Broker);
        AccessPolicy = new AiServerAccessPolicy(Settings, Docker.Connections);
        Recorder = new AiOperationRecorder(Docker.Journal);
    }

    public DockerTestGraph Docker { get; }

    public InMemoryAiBridgeSettingsStore SettingsStore { get; }

    public AiBridgeSettingsService Settings { get; }

    public ScriptedApprovalNotifier Notifier { get; }

    public AiApprovalBroker Broker { get; }

    public AiActionGate Gate { get; }

    public AiServerAccessPolicy AccessPolicy { get; }

    public AiOperationRecorder Recorder { get; }

    public void DisableApprovals()
    {
        SettingsStore.Stored = SettingsStore.Stored! with { RequireApproval = false };
    }

    public void DisableBridge()
    {
        SettingsStore.Stored = SettingsStore.Stored! with { Enabled = false };
    }

    public Task<SavedConnection> AddServerAsync(string name, CancellationToken cancellationToken)
    {
        var server = new TestServer(name, true, false);
        return AddAsync(server, cancellationToken);
    }

    public Task<SavedConnection> AddReadOnlyServerAsync(string name, CancellationToken cancellationToken)
    {
        var server = new TestServer(name, true, true);
        return AddAsync(server, cancellationToken);
    }

    public Task<SavedConnection> AddServerWithoutAiAccessAsync(string name, CancellationToken cancellationToken)
    {
        var server = new TestServer(name, false, false);
        return AddAsync(server, cancellationToken);
    }

    public Task<IReadOnlyList<Operation>> ListOperationsAsync(CancellationToken cancellationToken)
    {
        return Docker.ListOperationsAsync(cancellationToken);
    }

    private async Task<SavedConnection> AddAsync(TestServer server, CancellationToken cancellationToken)
    {
        var draft = new SavedConnectionDraft
        {
            Name = server.Name,
            Host = $"{server.Name}.example.test",
            UserName = "deploy",
            AuthMethod = ConnectionAuthMethod.Agent,
            AiAccess = server.AiAccess,
            ReadOnly = server.ReadOnly,
        };
        var connection = new SavedConnection { Id = Guid.NewGuid(), Details = draft };
        await Docker.Connections.AddAsync(connection, cancellationToken);
        if (server.ReadOnly)
        {
            Docker.Safety.MarkReadOnly(connection.Id);
        }

        return connection;
    }

    private readonly record struct TestServer(string Name, bool AiAccess, bool ReadOnly);
}

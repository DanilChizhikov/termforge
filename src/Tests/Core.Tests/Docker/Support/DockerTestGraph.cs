using DTech.Core.Diagnostics;
using DTech.Core.Diagnostics.Probes;
using DTech.Core.Docker;
using DTech.Core.Operations;
using DTech.Core.SavedConnections;
using DTech.Core.Tests.Diagnostics.Support;
using DTech.Core.Tests.Operations;
using DTech.Core.Tests.Safety.Support;
using DTech.Core.Tests.SavedConnections.Support;
using NSubstitute;

namespace DTech.Core.Tests.Docker.Support;

internal sealed class DockerTestGraph
{
    public DockerTestGraph()
    {
        Runner = new ScriptedCommandRunner();
        RunnerFactory = new ScriptedRunnerFactory(Runner);
        Connections = new InMemorySavedConnectionRepository();
        Operations = new InMemoryOperationRepository();
        StreamFactory = Substitute.For<IRemoteCommandStreamFactory>();
        var notifier = Substitute.For<IOperationNotifier>();
        var sanitizer = new OperationSanitizer();
        IOperationRetryHandler[] retryHandlers = [];
        Journal = new OperationJournal(Operations, sanitizer, notifier, retryHandlers, TimeProvider.System);
        var credentialStore = new InMemoryCredentialStore();
        var passwordStore = new ConnectionPasswordStore(credentialStore);
        TargetFactory = new ConnectionTargetFactory(passwordStore);
        TargetResolver = new DockerTargetResolver(Connections, TargetFactory);
        Safety = new WriteSafetyFixture();
    }

    public ScriptedCommandRunner Runner { get; }

    public ScriptedRunnerFactory RunnerFactory { get; }

    public InMemorySavedConnectionRepository Connections { get; }

    public InMemoryOperationRepository Operations { get; }

    public IRemoteCommandStreamFactory StreamFactory { get; }

    public OperationJournal Journal { get; }

    public ConnectionTargetFactory TargetFactory { get; }

    public DockerTargetResolver TargetResolver { get; }

    public WriteSafetyFixture Safety { get; }

    public DockerOverviewService CreateOverviewService()
    {
        var probe = new DockerProbe();
        var availabilityChecker = new DockerAvailabilityChecker(probe);
        var lister = new DockerContainerLister();
        return new DockerOverviewService(TargetResolver, RunnerFactory, availabilityChecker, lister);
    }

    public ContainerActionService CreateActionService()
    {
        var recorder = new DockerOperationRecorder(Journal);
        return new ContainerActionService(TargetResolver, RunnerFactory, recorder, Safety.Gate);
    }

    public ContainerLogService CreateLogService()
    {
        return new ContainerLogService(TargetResolver, StreamFactory);
    }

    public async Task<SavedConnection> AddConnectionAsync(string name, CancellationToken cancellationToken)
    {
        var draft = new SavedConnectionDraft
        {
            Name = name,
            Host = $"{name}.example.test",
            UserName = "deploy",
            AuthMethod = ConnectionAuthMethod.Agent,
        };
        var connection = new SavedConnection { Id = Guid.NewGuid(), Details = draft };
        await Connections.AddAsync(connection, cancellationToken);
        return connection;
    }

    public async Task<IReadOnlyList<Operation>> ListOperationsAsync(CancellationToken cancellationToken)
    {
        var page = await Journal.List(OperationQuery.Unfiltered, cancellationToken);
        return page.Items;
    }
}

using DTech.Core.Connections;
using DTech.Core.Editor;
using DTech.Core.Editor.Sudo;
using DTech.Core.SavedConnections;
using DTech.Core.Tests.SavedConnections.Support;
using NSubstitute;

namespace DTech.Core.Tests.Editor.Sudo;

internal sealed class SudoEditorTestGraph
{
    public const string ServerName = "web-01";

    public SudoEditorTestGraph()
    {
        Runner = new ScriptedShellRunner();
        Connections = new InMemorySavedConnectionRepository();
        Credentials = new InMemoryCredentialStore();
        var passwordStore = new ConnectionPasswordStore(Credentials);
        var targetFactory = new ConnectionTargetFactory(passwordStore);
        RunnerFactory = Substitute.For<IRemoteShellCommandRunnerFactory>();
        var connected = SshResult<IRemoteShellCommandRunner>.Success(Runner);
        RunnerFactory.ConnectAsync(Arg.Any<SshConnectionTarget>(), Arg.Any<CancellationToken>()).Returns(connected);
        var connector = new RemoteShellConnector(Connections, targetFactory, RunnerFactory);
        var commands = new SudoCommandBuilder();
        var classifier = new SudoResponseClassifier();
        var limits = new EditorLimits(EditorGraph.DefaultMaxFileBytes, EditorGraph.DefaultMaxBackups);
        Store = new SudoPasswordStore(Credentials);
        var resolver = new SudoPasswordResolver(Store);
        var shells = new SudoShellFactory(commands, limits);
        var authenticator = new SudoAuthenticator(classifier);
        Opener = new SudoEditorSessionOpener(connector, resolver, Store, shells, authenticator);
        var probe = new SudoAvailabilityProbe(commands, classifier);
        StatusReader = new SudoStatusReader(connector, probe, Store);
    }

    public ScriptedShellRunner Runner { get; }

    public InMemorySavedConnectionRepository Connections { get; }

    public InMemoryCredentialStore Credentials { get; }

    public IRemoteShellCommandRunnerFactory RunnerFactory { get; }

    public SudoPasswordStore Store { get; }

    public SudoEditorSessionOpener Opener { get; }

    public SudoStatusReader StatusReader { get; }

    public async Task<Guid> AddConnectionAsync(CancellationToken cancellationToken)
    {
        var draft = new SavedConnectionDraft
        {
            Name = ServerName,
            Host = "web-01.example.test",
            UserName = "deploy",
            AuthMethod = ConnectionAuthMethod.Agent,
        };
        var connection = new SavedConnection { Id = Guid.NewGuid(), Details = draft };
        await Connections.AddAsync(connection, cancellationToken);
        return connection.Id;
    }

    public void AnswerAuthentication(int exitCode, string standardError)
    {
        var result = new RemoteShellCommandResult(exitCode, [], standardError);
        Runner.Reply(static text => text.EndsWith(" true", StringComparison.Ordinal), result);
    }
}

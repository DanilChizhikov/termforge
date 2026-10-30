using System.Text;
using DTech.Core.AiBridge;
using DTech.Core.AiBridge.Approvals;
using DTech.Core.AiBridge.Terminals;
using DTech.Core.Connections;
using DTech.Core.SavedConnections;
using DTech.Core.Terminal;
using DTech.Core.Terminal.Sessions;
using DTech.Core.Tests.AiBridge.Support;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.AiBridge;

[TestClass]
public sealed class AiTerminalInputSenderTests
{
    private static readonly SshConnectionTarget Target = new(new SshEndpoint("example.test", 22), "deploy", new AgentAuthentication(null));

    private AiBridgeTestGraph _graph = null!;
    private TerminalSessionManager _manager = null!;
    private ITerminalSession _transport = null!;
    private AiTerminalLocator _locator = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _graph = new AiBridgeTestGraph();
        _transport = Substitute.For<ITerminalSession>();
        _transport.WriteAsync(default, default).ReturnsForAnyArgs(ValueTask.FromResult(SshResult.Success()));
        var factory = Substitute.For<ITerminalSessionFactory>();
        factory.OpenAsync(default!, default).ReturnsForAnyArgs(_ => SshResult<ITerminalSession>.Success(_transport));
        _manager = new TerminalSessionManager(factory, TimeProvider.System, TerminalSessionOptions.Default);
        _locator = new AiTerminalLocator(_manager, _graph.AccessPolicy);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _manager.DisposeAsync();
    }

    [TestMethod]
    public async Task Send_Approved_WritesTextAndEnterToTheTerminal()
    {
        var session = await OpenTerminalAsync(await _graph.AddServerAsync("prod-web", TestContext.CancellationToken));
        var sender = new AiTerminalInputSender(_locator, _graph.Gate, _graph.Recorder);

        var input = new AiTerminalInput(session.Descriptor.Id.Value, "ls -la", true);
        var sent = await sender.SendAsync(input, TestContext.CancellationToken);

        sent.Value.Id.Should().Be(session.Descriptor.Id);
        var expected = Encoding.UTF8.GetBytes("ls -la\r");
        await _transport.Received(1).WriteAsync(Arg.Is<ReadOnlyMemory<byte>>(data => data.ToArray().SequenceEqual(expected)), Arg.Any<CancellationToken>());
        var operation = (await _graph.ListOperationsAsync(TestContext.CancellationToken)).Should().ContainSingle().Subject;
        operation.Kind.Should().Be(AiOperationRecorder.TerminalInputKind);
    }

    [TestMethod]
    public async Task Send_Denied_DoesNotWrite()
    {
        var session = await OpenTerminalAsync(await _graph.AddServerAsync("prod-web", TestContext.CancellationToken));
        _graph.Notifier.Answer = AiApprovalOutcome.Denied;
        var sender = new AiTerminalInputSender(_locator, _graph.Gate, _graph.Recorder);

        var input = new AiTerminalInput(session.Descriptor.Id.Value, "reboot", true);
        var sent = await sender.SendAsync(input, TestContext.CancellationToken);

        sent.Error!.Code.Should().Be(AiBridgeErrorCode.Denied);
        await _transport.DidNotReceiveWithAnyArgs().WriteAsync(default, default);
    }

    [TestMethod]
    public async Task Send_ReadOnlyServer_IsBlocked()
    {
        var session = await OpenTerminalAsync(await _graph.AddReadOnlyServerAsync("prod-db", TestContext.CancellationToken));
        var sender = new AiTerminalInputSender(_locator, _graph.Gate, _graph.Recorder);

        var input = new AiTerminalInput(session.Descriptor.Id.Value, "ls", true);
        var sent = await sender.SendAsync(input, TestContext.CancellationToken);

        sent.Error!.Code.Should().Be(AiBridgeErrorCode.ReadOnlyServer);
        _graph.Notifier.Requested.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Send_UnknownTerminal_ReturnsNotFound()
    {
        var sender = new AiTerminalInputSender(_locator, _graph.Gate, _graph.Recorder);

        var input = new AiTerminalInput("missing", "ls", true);
        var sent = await sender.SendAsync(input, TestContext.CancellationToken);

        sent.Error!.Code.Should().Be(AiBridgeErrorCode.NotFound);
    }

    [TestMethod]
    public async Task List_ReturnsOnlyTerminalsOfServersWithAiAccess()
    {
        var open = await OpenTerminalAsync(await _graph.AddServerAsync("prod-web", TestContext.CancellationToken));
        await OpenTerminalAsync(await _graph.AddServerWithoutAiAccessAsync("private", TestContext.CancellationToken));

        var listed = await _locator.ListAsync(TestContext.CancellationToken);

        listed.Value.Should().ContainSingle().Which.Id.Should().Be(open.Descriptor.Id);
    }

    [TestMethod]
    public async Task ReadOutput_ReturnsPlainTextTail()
    {
        var session = await OpenTerminalAsync(await _graph.AddServerAsync("prod-web", TestContext.CancellationToken));
        session.OutputTail.Append("\u001b[32mok\u001b[0m\r\ndone\r\n"u8);
        var reader = new AiTerminalOutputReader(_locator);

        var request = new AiTerminalOutputRequest(session.Descriptor.Id.Value, 5);
        var read = await reader.ReadAsync(request, TestContext.CancellationToken);

        read.Value.Text.Should().Be("done\n");
        read.Value.Truncated.Should().BeTrue();
    }

    private async Task<ManagedTerminalSession> OpenTerminalAsync(SavedConnection server)
    {
        var terminalServer = new TerminalServer(server.Id, server.Details.Name, Target);
        var request = new TerminalOpenRequest("owner", terminalServer, new TerminalSize(80, 24));
        var opened = await _manager.OpenAsync(request, TestContext.CancellationToken);
        return opened.Value;
    }
}

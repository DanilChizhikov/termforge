using DTech.Core.Connections;
using DTech.Core.Files;
using DTech.Core.Files.Local;
using DTech.Core.Files.Remote;
using DTech.Core.Tests.Safety.Support;
using DTech.Core.Transfers;
using DTech.Core.Transfers.Execution;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Transfers;

[TestClass]
public sealed class RemoteTransferStrategyTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Copy_LargeStream_UsesDestinationProgressAndPreservesBytes()
    {
        var fixture = new Fixture();
        var bytes = new byte[4 * 1024 * 1024];
        Random.Shared.NextBytes(bytes);
        fixture.SetSource(bytes);
        using var received = new MemoryStream();
        fixture.Target.WriteFileAsync(Arg.Any<RemoteWriteRequest>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var request = call.Arg<RemoteWriteRequest>();
            await request.Source.CopyToAsync(received, call.Arg<CancellationToken>());
            return SshResult.Success();
        });
        var copy = fixture.Copy(bytes.Length);

        var result = await fixture.Strategy.CopyAsync(fixture.Context, copy, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        received.ToArray().Should().Equal(bytes);
    }

    [TestMethod]
    public async Task Copy_DestinationFails_CancelsBlockedSource()
    {
        var fixture = new Fixture();
        fixture.SetSource(new byte[4 * 1024 * 1024]);
        var error = new SshError(SshErrorKind.ConnectionFailed, "destination lost");
        fixture.Target.WriteFileAsync(Arg.Any<RemoteWriteRequest>(), Arg.Any<CancellationToken>()).Returns(SshResult.Failure(error));
        var copy = fixture.Copy(4 * 1024 * 1024);

        var task = fixture.Strategy.CopyAsync(fixture.Context, copy, TestContext.CancellationToken);
        var result = await task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(FileErrorCode.ConnectionFailed);
    }

    [TestMethod]
    public async Task Copy_SourceFails_CancelsWaitingDestination()
    {
        var fixture = new Fixture();
        fixture.SetSource(new byte[100]);
        var error = new SshError(SshErrorKind.ConnectionFailed, "source lost");
        fixture.Source.ReadFileAsync(Arg.Any<RemoteReadRequest>(), Arg.Any<CancellationToken>()).Returns(SshResult.Failure(error));
        fixture.Target.WriteFileAsync(Arg.Any<RemoteWriteRequest>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var request = call.Arg<RemoteWriteRequest>();
            await request.Source.CopyToAsync(Stream.Null, call.Arg<CancellationToken>());
            return SshResult.Success();
        });

        var task = fixture.Strategy.CopyAsync(fixture.Context, fixture.Copy(100), TestContext.CancellationToken);
        var result = await task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        result.IsSuccess.Should().BeFalse();
    }

    [TestMethod]
    public async Task Verify_MatchingPrefix_AllowsResume()
    {
        var fixture = new Fixture();
        fixture.SetSource([1, 2, 3, 4]);
        fixture.Target.ReadFileAsync(Arg.Any<RemoteReadRequest>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var read = call.Arg<RemoteReadRequest>();
            await read.Destination.WriteAsync(new byte[] { 1, 2 }, call.Arg<CancellationToken>());
            return SshResult.Success();
        });
        var verifier = new RemoteTransferVerifier();
        var copy = fixture.Copy(4) with { Offset = 2 };

        var result = await verifier.VerifyAsync(fixture.Context, copy, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }

    [TestMethod]
    public async Task Inspect_SameServerRecursiveTarget_IsRejected()
    {
        var fixture = new Fixture();
        var request = fixture.Context.Request with { TargetConnectionId = fixture.Context.Request.ConnectionId, SourcePath = "/srv", TargetDirectory = "/srv/nested" };
        var context = fixture.Context with { Request = request };

        var result = await fixture.Strategy.InspectAsync(context, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(FileErrorCode.Invalid);
    }

    private sealed class Fixture
    {
        private static readonly DateTime Modified = new(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);
        public Fixture()
        {
            var paths = new RemotePathNormalizer();
            var localPaths = new LocalPathResolver();
            var planner = new LocalTransferPlanner();
            var upload = new UploadTransferStrategy(localPaths, paths, planner);
            var remotePlanner = new RemoteTransferPlanner();
            var safety = new WriteSafetyFixture();
            var verifier = new RemoteTransferVerifier();
            Strategy = new RemoteToRemoteTransferStrategy(paths, remotePlanner, upload, safety.Gate, verifier);
            var request = new TransferRequest
            {
                ConnectionId = Guid.NewGuid(), TargetConnectionId = Guid.NewGuid(), ServerName = "source",
                Direction = TransferDirection.RemoteToRemote, SourcePath = "/a", TargetDirectory = "/srv",
            };
            Context = new TransferContext(Source, request) { TargetClient = Target };
        }
        public IRemoteFileClient Source { get; } = Substitute.For<IRemoteFileClient>();
        public IRemoteFileClient Target { get; } = Substitute.For<IRemoteFileClient>();
        public TransferContext Context { get; }
        public RemoteToRemoteTransferStrategy Strategy { get; }
        public void SetSource(byte[] bytes)
        {
            var entry = new RemoteFileEntry("a", "/a", RemoteFileKind.File) { Size = bytes.Length, LastWriteTimeUtc = Modified };
            Source.StatAsync("/a", Arg.Any<CancellationToken>()).Returns(SshResult<RemoteFileEntry>.Success(entry));
            Source.ReadFileAsync(Arg.Any<RemoteReadRequest>(), Arg.Any<CancellationToken>()).Returns(async call =>
            {
                var read = call.Arg<RemoteReadRequest>();
                var memory = bytes.AsMemory((int)read.Offset);
                await read.Destination.WriteAsync(memory, call.Arg<CancellationToken>());
                return SshResult.Success();
            });
        }
        public TransferCopy Copy(long size)
        {
            var item = new TransferItem("/a", "/srv/a", false) { Size = size, ModifiedUtc = Modified };
            return new TransferCopy(item, "/srv/.a.part", 0);
        }
    }
}

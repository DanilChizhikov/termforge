using DTech.Core.Files;
using DTech.Ssh.Files;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.Extensions;
using Renci.SshNet;

namespace DTech.Ssh.IntegrationTests;

[TestClass]
public sealed class SftpFileReplacerTests
{
    private const string Source = "/data/.upload.part";
    private const string Target = "/data/document.txt";

    [TestMethod]
    public async Task Replace_WithPosixRename_DoesNotMoveOrDeleteOriginal()
    {
        var client = Substitute.For<ISftpClient>();

        await new SftpFileReplacer(client).ReplaceAsync(new RemoteRenameRequest(Source, Target), CancellationToken.None);

        client.Received(1).RenameFile(Source, Target, true);
        await client.DidNotReceiveWithAnyArgs().RenameFileAsync(default!, default!, default);
        await client.DidNotReceiveWithAnyArgs().DeleteFileAsync(default!, default);
    }

    [TestMethod]
    public async Task Replace_WithoutExistingTarget_PlacesSourceDirectly()
    {
        var client = WithoutPosixRename();

        await new SftpFileReplacer(client).ReplaceAsync(new RemoteRenameRequest(Source, Target), CancellationToken.None);

        await client.Received(1).RenameFileAsync(Source, Target, CancellationToken.None);
        await client.DidNotReceiveWithAnyArgs().DeleteFileAsync(default!, default);
    }

    [TestMethod]
    public async Task Replace_WithExistingTarget_RemovesAsideOnlyAfterPlacement()
    {
        var client = WithoutPosixRename();
        var files = new Dictionary<string, string> { [Source] = "new", [Target] = "original" };
        TrackFiles(client, files);
        client.DeleteFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            files[Target].Should().Be("new");
            files.Remove(call.ArgAt<string>(0)).Should().BeTrue();
            return Task.CompletedTask;
        });

        await new SftpFileReplacer(client).ReplaceAsync(new RemoteRenameRequest(Source, Target), CancellationToken.None);

        files.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, string>(Target, "new"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Replace_WhenPlacementFails_RestoresOriginalEvenIfCancelled(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var client = WithoutPosixRename();
        var files = new Dictionary<string, string> { [Source] = "new", [Target] = "original" };
        TrackFiles(client, files);
        client.Configure().RenameFileAsync(Source, Target, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (cancel)
            {
                cancellation.Cancel();
                return Task.FromCanceled(cancellation.Token);
            }

            return Task.FromException(new IOException("Placement failed"));
        });

        var replace = () => new SftpFileReplacer(client).ReplaceAsync(new RemoteRenameRequest(Source, Target), cancellation.Token);

        if (cancel)
        {
            await replace.Should().ThrowAsync<OperationCanceledException>();
        }
        else
        {
            await replace.Should().ThrowAsync<IOException>().WithMessage("Placement failed");
        }

        files.Should().HaveCount(2);
        files[Target].Should().Be("original");
        files[Source].Should().Be("new");
        await client.DidNotReceiveWithAnyArgs().DeleteFileAsync(default!, default);
        await client.Received().RenameFileAsync(Arg.Is<string>(path => path.EndsWith(".old")), Target, CancellationToken.None);
    }

    [TestMethod]
    public async Task Replace_WhenRestoreFails_PreservesAsideAndReportsItsPath()
    {
        var client = WithoutPosixRename();
        var files = new Dictionary<string, string> { [Source] = "new", [Target] = "original" };
        TrackFiles(client, files);
        client.Configure().RenameFileAsync(Source, Target, Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("Placement failed"));
        client.Configure().RenameFileAsync(Arg.Is<string>(path => path.EndsWith(".old")), Target, Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("Restore failed"));

        var replace = () => new SftpFileReplacer(client).ReplaceAsync(new RemoteRenameRequest(Source, Target), CancellationToken.None);

        var failure = await replace.Should().ThrowAsync<IOException>();
        var aside = files.Single(pair => pair.Value == "original").Key;
        failure.Which.Message.Should().Contain(aside);
        failure.Which.InnerException.Should().BeOfType<AggregateException>();
        files.Should().NotContainKey(Target);
        await client.DidNotReceiveWithAnyArgs().DeleteFileAsync(default!, default);
    }

    private static ISftpClient WithoutPosixRename()
    {
        var client = Substitute.For<ISftpClient>();
        client.When(c => c.RenameFile(Source, Target, true)).Do(_ => throw new NotSupportedException());
        return client;
    }

    private static void TrackFiles(ISftpClient client, Dictionary<string, string> files)
    {
        client.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => files.ContainsKey(call.ArgAt<string>(0)));
        client.RenameFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var from = call.ArgAt<string>(0);
            var to = call.ArgAt<string>(1);
            files.Add(to, files[from]);
            files.Remove(from);
            return Task.CompletedTask;
        });
    }
}

using DTech.Core.Diagnostics;
using DTech.Core.Docker;
using DTech.Core.Tests.Docker.Support;
using FluentAssertions;

namespace DTech.Core.Tests.Docker;

[TestClass]
public sealed class DockerFailureClassifierTests
{
    [TestMethod]
    public void Classify_CommandNotFound_IsDockerMissing()
    {
        var result = new RemoteCommandResult(127, string.Empty, DockerRecordedOutputs.CommandNotFound);

        var error = DockerFailureClassifier.Classify(result);

        error.Code.Should().Be(DockerErrorCode.DockerMissing);
        error.Message.Should().Contain("not installed");
    }

    [TestMethod]
    public void Classify_SocketPermissionDenied_ExplainsDockerGroup()
    {
        var result = new RemoteCommandResult(1, string.Empty, DockerRecordedOutputs.PermissionDenied);

        var error = DockerFailureClassifier.Classify(result);

        error.Code.Should().Be(DockerErrorCode.PermissionDenied);
        error.Message.Should().Contain("docker group");
    }

    [TestMethod]
    public void Classify_DaemonDown_IsDaemonUnavailable()
    {
        var result = new RemoteCommandResult(1, string.Empty, DockerRecordedOutputs.DaemonDown);

        var error = DockerFailureClassifier.Classify(result);

        error.Code.Should().Be(DockerErrorCode.DaemonUnavailable);
    }

    [TestMethod]
    public void Classify_NoSuchContainer_IsContainerNotFound()
    {
        var result = new RemoteCommandResult(1, string.Empty, DockerRecordedOutputs.NoSuchContainer);

        var error = DockerFailureClassifier.Classify(result);

        error.Code.Should().Be(DockerErrorCode.ContainerNotFound);
    }

    [TestMethod]
    public void Classify_OtherFailure_IncludesExitCodeAndDaemonMessage()
    {
        var result = new RemoteCommandResult(1, string.Empty, DockerRecordedOutputs.StopTimeoutFailure);

        var error = DockerFailureClassifier.Classify(result);

        error.Code.Should().Be(DockerErrorCode.CommandFailed);
        error.Message.Should().Contain("code 1").And.Contain("cannot stop container");
    }

    [TestMethod]
    [DataRow("Error response from daemon: No such image: nginx:9", DockerErrorCode.ImageNotFound)]
    [DataRow("Error response from daemon: conflict: unable to remove repository reference \"nginx\" (must force) - container 3f4e is using its referenced image", DockerErrorCode.ImageInUse)]
    [DataRow("Error response from daemon: conflict: unable to delete 3f4e (cannot be forced) - image is being used by running container 9a1b", DockerErrorCode.ImageInUse)]
    [DataRow("docker: 'compose' is not a docker command.", DockerErrorCode.ComposeMissing)]
    [DataRow("unknown command: docker compose", DockerErrorCode.ComposeMissing)]
    public void Classify_ImageAndComposeFailures(string standardError, DockerErrorCode expected)
    {
        var result = new RemoteCommandResult(1, string.Empty, standardError);

        var error = DockerFailureClassifier.Classify(result);

        error.Code.Should().Be(expected);
    }
}

using DTech.Core.Connections;
using DTech.Core.Diagnostics;
using DTech.Core.Tests.Diagnostics.Support;

namespace DTech.Core.Tests.Docker.Support;

internal sealed class ScriptedRunnerFactory(ScriptedCommandRunner runner) : IRemoteCommandRunnerFactory
{
    public int Connections { get; private set; }

    public SshError? ConnectionError { get; set; }

    public Task<SshResult<IRemoteCommandRunner>> ConnectAsync(SshConnectionTarget target, CancellationToken cancellationToken)
    {
        Connections++;
        if (ConnectionError is not null)
        {
            var failure = SshResult<IRemoteCommandRunner>.Failure(ConnectionError);
            return Task.FromResult(failure);
        }

        var connected = SshResult<IRemoteCommandRunner>.Success(runner);
        return Task.FromResult(connected);
    }
}

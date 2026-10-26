using DTech.Core.Connections;

namespace DTech.Core.Diagnostics;

public static class ProbeCommands
{
    public static Task<SshResult<RemoteCommandResult>> RunScriptAsync(this IRemoteCommandRunner runner, string script, CancellationToken cancellationToken)
    {
        var command = ShellScript.Wrap(script);
        var request = RemoteCommandRequest.WithDefaults(command);
        return runner.RunAsync(request, cancellationToken);
    }
}

using DTech.Core.Diagnostics;

namespace DTech.Core.NetworkDiagnostics;

public sealed class RemoteNetworkProbeSession(IRemoteCommandRunner runner) : INetworkProbeSession
{
    private HashSet<string> _tools = [];
    private bool _linux;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var wrapped = ShellScript.Wrap(RemoteNetworkCommands.Discover);
        var request = RemoteCommandRequest.WithDefaults(wrapped);
        var result = await runner.RunAsync(request, cancellationToken);
        if (!result.IsSuccess) throw new IOException(result.Error!.Message);
        var lines = RemoteText.SplitLines(result.Value.StandardOutput);
        _linux = lines.Count > 0 && lines[0].Trim() == "Linux";
        _tools = lines.Skip(1).Select(line => line.Trim()).ToHashSet(StringComparer.Ordinal);
    }

    public async Task<NetworkProbeOutcome> ProbeAsync(NetworkProbe probe, CancellationToken cancellationToken)
    {
        var tool = RemoteNetworkCommands.Tool(probe.Protocol);
        if (!_linux || !_tools.Contains(tool) || probe.Protocol == "tls" && !_tools.Contains("timeout"))
            return new NetworkProbeOutcome(NetworkProbeOutcome.Unavailable, !_linux ? "Only Linux SSH points are supported." : $"Missing {tool} or its timeout utility. Installation requires explicit confirmation.");
        var command = RemoteNetworkCommands.Create(probe);
        var wrapped = ShellScript.Wrap($"export LC_ALL=C; {command}");
        var timeout = TimeSpan.FromSeconds(probe.Protocol is "http" or "route" ? 12 : 5);
        var request = new RemoteCommandRequest(wrapped, timeout, 16 * 1024);
        var result = await runner.RunAsync(request, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!result.IsSuccess)
        {
            var timedOut = result.Error!.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase);
            return new NetworkProbeOutcome(timedOut ? NetworkProbeOutcome.NoResponse : NetworkProbeOutcome.Error, RemoteText.Sanitize(result.Error.Message, 1000));
        }
        return RemoteNetworkCommands.Parse(probe, result.Value);
    }

    public ValueTask DisposeAsync() => runner.DisposeAsync();
}

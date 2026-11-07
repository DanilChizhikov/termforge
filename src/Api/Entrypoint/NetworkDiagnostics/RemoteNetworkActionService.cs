using System.Globalization;
using System.Xml.Linq;
using DTech.Core.Credentials;
using DTech.Core.Diagnostics;
using DTech.Core.Editor.Sudo;
using DTech.Core.NetworkDiagnostics;
using DTech.Core.Safety;
using DTech.Core.SavedConnections;

namespace DTech.Entrypoint.NetworkDiagnostics;

public sealed class RemoteNetworkActionService
{
    private readonly ISavedConnectionRepository _connections;
    private readonly RemoteShellConnector _shells;
    private readonly ServerWriteGate _writeGate;
    private readonly IConfirmationTokenStore _confirmations;
    private readonly RemoteNetworkActionPlans _plans;

    public RemoteNetworkActionService(ISavedConnectionRepository connections, RemoteShellConnector shells, ServerWriteGate writeGate, IConfirmationTokenStore confirmations, RemoteNetworkActionPlans plans)
    {
        _connections = connections;
        _shells = shells;
        _writeGate = writeGate;
        _confirmations = confirmations;
        _plans = plans;
    }

    public async Task<RemoteNetworkActionPlan> PrepareAsync(RemoteNetworkActionRequest request, CancellationToken cancellationToken)
    {
        var saved = await RequireWritableAsync(request.ConnectionId, cancellationToken);
        var opened = await _shells.ConnectAsync(request.ConnectionId, cancellationToken);
        if (!opened.IsSuccess) throw new IOException(opened.Error.Message);
        await using var runner = opened.Value.Runner;
        var discovery = ShellScript.Wrap(NetworkPackageInstaller.Discover);
        var command = new RemoteShellCommand(discovery, TimeSpan.FromSeconds(10), 4096);
        var discovered = await runner.RunAsync(command, cancellationToken);
        if (!discovered.IsSuccess || discovered.Value.ExitCode != 0) throw new IOException("Cannot discover the remote system and package manager.");
        var lines = RemoteText.SplitLines(discovered.Value.StandardOutputText).Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
        if (lines.Length < 2 || lines[0] != "Linux") throw new ArgumentException("Only Linux SSH points are supported.");
        var root = lines[^1] == "0";
        string text;
        IReadOnlyList<string> packages = [];
        if (request.Kind == "install")
        {
            var manager = lines.Skip(1).FirstOrDefault(line => line is "apt-get" or "dnf" or "apk" or "pacman" or "zypper");
            if (manager is null) throw new ArgumentException("Unsupported package manager. Install the diagnostic utilities manually from official repositories.");
            if (request.Protocols is null) throw new ArgumentException("Select utilities to install.");
            var installation = NetworkPackageInstaller.Create(manager, request.Protocols);
            packages = installation.Packages;
            text = installation.Command;
        }
        else if (request.Kind == "udpDetails")
        {
            var validation = new NetworkDiagnosticRequest
            {
                Host = request.Host, Ports = request.Port?.ToString(CultureInfo.InvariantCulture),
                Protocols = ["udp"], Points = [new NetworkDiagnosticPoint(request.ConnectionId, "rf")],
            };
            var plan = NetworkDiagnosticPlanner.Create(validation);
            if (request.Port is null) throw new ArgumentException("A single UDP port is required.");
            var host = ShellArgument.Quote(plan.Host);
            text = $"nmap -sU -n -Pn --max-retries 0 --host-timeout 5s -oX - -p {request.Port} -- {host}";
        }
        else throw new ArgumentException("Only install and udpDetails actions are supported.");
        var requiresSudo = !root;
        if (requiresSudo)
        {
            var wrapped = ShellScript.Wrap(text);
            text = request.UsePassword ? $"sudo -S -p '' {wrapped}" : $"sudo -n {wrapped}";
        }
        var id = Guid.NewGuid();
        var target = id.ToString("N");
        var scope = new ConfirmationScope(DangerousOperation.NetworkRemoteAction, request.ConnectionId, target);
        var grant = _confirmations.Issue(scope);
        var action = new RemoteNetworkActionPlan
        {
            Id = id, ConnectionId = request.ConnectionId, ServerName = saved.Details.Name, Kind = request.Kind,
            Packages = packages, Command = text, RequiresSudo = requiresSudo, ConfirmationToken = grant.Token, ExpiresAt = grant.ExpiresAt,
        };
        var pending = new PendingNetworkAction(action, saved.UpdatedAt, request.UsePassword && requiresSudo);
        _plans.Add(pending);
        return action;
    }

    public async Task<RemoteNetworkActionResult> ExecuteAsync(RemoteNetworkActionExecution request, CancellationToken cancellationToken)
    {
        var pending = _plans.Find(request.PlanId) ?? throw new ArgumentException("Prepare the action again; this plan is missing or expired.");
        var plan = pending.Plan;
        var saved = await RequireWritableAsync(plan.ConnectionId, cancellationToken);
        if (saved.UpdatedAt != pending.ConnectionRevision) throw new ArgumentException("The saved connection changed. Prepare and confirm the action again.");
        if (pending.UsePassword != !string.IsNullOrEmpty(request.Password)) throw new ArgumentException("The sudo password mode must match the prepared plan.");
        var target = plan.Id.ToString("N");
        var scope = new ConfirmationScope(DangerousOperation.NetworkRemoteAction, plan.ConnectionId, target);
        if (!_confirmations.TryConsume(scope, request.ConfirmationToken) || !_plans.Remove(plan.Id))
            throw new UnauthorizedAccessException("A fresh confirmation for this exact server action is required.");
        var opened = await _shells.ConnectAsync(plan.ConnectionId, cancellationToken);
        if (!opened.IsSuccess) throw new IOException(opened.Error.Message);
        await using var runner = opened.Value.Runner;
        using var password = request.Password is null ? null : Secret.FromString(request.Password);
        using var input = password is null ? null : SudoPasswordInput.From(password);
        var wrapped = ShellScript.Wrap(plan.Command);
        var rechecked = await RequireWritableAsync(plan.ConnectionId, cancellationToken);
        if (rechecked.UpdatedAt != pending.ConnectionRevision) throw new ArgumentException("The saved connection changed. Prepare the action again.");
        var timeout = TimeSpan.FromSeconds(plan.Kind == "install" ? 300 : 10);
        var command = new RemoteShellCommand(wrapped, timeout, 64 * 1024) { StandardInput = input?.Line ?? ReadOnlyMemory<byte>.Empty };
        var result = await runner.RunAsync(command, cancellationToken);
        NetworkProbeOutcome outcome;
        if (!result.IsSuccess) outcome = new NetworkProbeOutcome(NetworkProbeOutcome.Error, "The remote action failed or was cancelled. Recheck the server before retrying.");
        else if (result.Value.OutputTruncated) outcome = new NetworkProbeOutcome(NetworkProbeOutcome.Error, "The remote action exceeded its output limit. Recheck the server before retrying.");
        else if (result.Value.ExitCode != 0)
        {
            var detail = result.Value.StandardError;
            if (!string.IsNullOrEmpty(request.Password)) detail = detail.Replace(request.Password, Secret.Redacted, StringComparison.Ordinal);
            outcome = new NetworkProbeOutcome(NetworkProbeOutcome.Error, RemoteText.Sanitize(detail, 1000));
        }
        else outcome = plan.Kind == "install" ? new NetworkProbeOutcome(NetworkProbeOutcome.Responded, "Package installation completed.") : ParseUdp(result.Value.StandardOutputText);
        var discoverText = ShellScript.Wrap(RemoteNetworkCommands.Discover);
        var discover = new RemoteShellCommand(discoverText, TimeSpan.FromSeconds(10), 4096);
        var capabilities = await runner.RunAsync(discover, cancellationToken);
        var tools = capabilities.IsSuccess ? RemoteText.Sanitize(capabilities.Value.StandardOutputText, 2000) : "Unable to refresh available utilities.";
        return new RemoteNetworkActionResult(outcome, tools);
    }

    private async Task<SavedConnection> RequireWritableAsync(Guid id, CancellationToken cancellationToken)
    {
        var saved = await _connections.FindAsync(id, cancellationToken) ?? throw new ArgumentException("The saved SSH point was not found.");
        if (await _writeGate.IsReadOnlyAsync(id, cancellationToken)) throw new UnauthorizedAccessException(ServerWriteGate.ReadOnlyMessage);
        return saved;
    }

    private static NetworkProbeOutcome ParseUdp(string xml)
    {
        try
        {
            var document = XDocument.Parse(xml);
            var state = document.Descendants("port").Elements("state").Select(element => (string?)element.Attribute("state")).FirstOrDefault();
            return state switch
            {
                "open" => new NetworkProbeOutcome(NetworkProbeOutcome.Responded, "Nmap received a UDP response."),
                "closed" => new NetworkProbeOutcome(NetworkProbeOutcome.Refused, "Nmap received a UDP port refusal."),
                "open|filtered" or "filtered" => new NetworkProbeOutcome(NetworkProbeOutcome.NoResponse, "No conclusive UDP response. This does not establish network blocking."),
                _ => new NetworkProbeOutcome(NetworkProbeOutcome.Error, "Nmap did not return a conclusive port state."),
            };
        }
        catch (System.Xml.XmlException) { return new NetworkProbeOutcome(NetworkProbeOutcome.Error, "Nmap returned invalid XML."); }
    }
}

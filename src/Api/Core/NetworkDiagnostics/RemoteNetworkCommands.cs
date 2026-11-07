using System.Globalization;
using DTech.Core.Diagnostics;

namespace DTech.Core.NetworkDiagnostics;

public static class RemoteNetworkCommands
{
    public const string Discover = "uname -s; for tool in nc dig ssh-keyscan openssl curl traceroute timeout nmap; do if command -v \"$tool\" >/dev/null 2>&1; then printf '%s\\n' \"$tool\"; fi; done";

    public static string Tool(string protocol) => protocol switch
    {
        "dns" => "dig", "tcp" or "udp" => "nc", "ssh" => "ssh-keyscan", "tls" => "openssl", "http" => "curl", "route" => "traceroute",
        _ => throw new ArgumentException("Unsupported protocol."),
    };

    public static string Create(NetworkProbe probe)
    {
        var host = ShellArgument.Quote(probe.Host);
        var domain = ShellArgument.Quote(probe.Domain);
        var port = probe.Port?.ToString(CultureInfo.InvariantCulture);
        var endpoint = probe.Host.Contains(':') ? $"[{probe.Host}]:{port}" : $"{probe.Host}:{port}";
        var quotedEndpoint = ShellArgument.Quote(endpoint);
        var verifyName = System.Net.IPAddress.TryParse(probe.Domain, out _) ? "-verify_ip" : "-verify_hostname";
        var httpUrl = probe.Url ?? new UriBuilder(probe.Port == 443 ? "https" : "http", probe.Host, probe.Port ?? 80).Uri;
        var url = ShellArgument.Quote(httpUrl.AbsoluteUri);
        return probe.Protocol switch
        {
            "dns" => $"dig +time=3 +tries=1 {domain}",
            "tcp" => $"nc -zv -w 3 -- {host} {port}",
            "udp" => $"printf '\\000' | nc -u -v -w 3 -- {host} {port}",
            "ssh" => $"ssh-keyscan -T 3 -p {port} {host}",
            "tls" => $"timeout 3 openssl s_client -connect {quotedEndpoint} -servername {domain} {verifyName} {domain} -verify_return_error </dev/null",
            "http" => $"curl --noproxy '*' --silent --show-error --max-time 10 --output /dev/null --write-out '%{{http_code}}' -- {url}",
            "route" => $"traceroute -n -m 12 -w 1 -- {host}",
            _ => throw new ArgumentException("Unsupported protocol."),
        };
    }

    public static NetworkProbeOutcome Parse(NetworkProbe probe, RemoteCommandResult result)
    {
        var text = result.CombinedOutput;
        if (result.OutputTruncated) return new NetworkProbeOutcome(NetworkProbeOutcome.Error, "The check exceeded its output limit.");
        if (text.Contains("Connection refused", StringComparison.OrdinalIgnoreCase)) return new NetworkProbeOutcome(NetworkProbeOutcome.Refused, "The remote point received a connection refusal.");
        if (result.ExitCode == 124 || text.Contains("timed out", StringComparison.OrdinalIgnoreCase)) return new NetworkProbeOutcome(NetworkProbeOutcome.NoResponse, "No response before the timeout. This does not establish network blocking.");
        if (text.Contains("not found", StringComparison.OrdinalIgnoreCase) || text.Contains("Operation not permitted", StringComparison.OrdinalIgnoreCase)
            || text.Contains("invalid option", StringComparison.OrdinalIgnoreCase) || text.Contains("unrecognized option", StringComparison.OrdinalIgnoreCase))
            return new NetworkProbeOutcome(NetworkProbeOutcome.Unavailable, "The required tool or permission is unavailable. Installation and elevated actions require separate confirmation.");
        if (probe.Protocol == "udp")
        {
            if (result.ExitCode != 0 && !string.IsNullOrWhiteSpace(result.StandardError))
                return new NetworkProbeOutcome(NetworkProbeOutcome.Error, NetworkDiagnosticText.Format(result.StandardError, 1000));
            return string.IsNullOrEmpty(result.StandardOutput)
                ? new NetworkProbeOutcome(NetworkProbeOutcome.NoResponse, "No UDP reply was received. An open port and a filtered port cannot be distinguished from silence.")
                : new NetworkProbeOutcome(NetworkProbeOutcome.Responded, "The remote point received UDP data.");
        }
        if (result.ExitCode != 0)
        {
            var status = probe.Protocol is "tcp" or "ssh" && string.IsNullOrWhiteSpace(text) ? NetworkProbeOutcome.NoResponse : NetworkProbeOutcome.Error;
            return new NetworkProbeOutcome(status, NetworkDiagnosticText.Format(text, 1000));
        }
        if (probe.Protocol == "http")
        {
            var code = result.StandardOutput.Trim();
            return int.TryParse(code, out var status) && status is >= 100 and <= 599
                ? new NetworkProbeOutcome(NetworkProbeOutcome.Responded, $"HTTP {code}; redirects were not followed.")
                : new NetworkProbeOutcome(NetworkProbeOutcome.Error, "The HTTP tool did not report a valid status.");
        }
        if (probe.Protocol == "ssh")
        {
            return string.IsNullOrWhiteSpace(result.StandardOutput)
                ? new NetworkProbeOutcome(NetworkProbeOutcome.NoResponse, "No SSH host key was received.")
                : new NetworkProbeOutcome(NetworkProbeOutcome.Responded, "SSH key exchange completed; host key collected without login. This check does not establish host trust.");
        }
        if (probe.Protocol == "route" && result.StandardOutput.Contains('*'))
            return new NetworkProbeOutcome(NetworkProbeOutcome.NoResponse, NetworkDiagnosticText.Format(result.StandardOutput, 2000));
        return new NetworkProbeOutcome(NetworkProbeOutcome.Responded, probe.Protocol == "tls"
            ? "TLS handshake and certificate validation succeeded." : NetworkDiagnosticText.Format(text, 2000));
    }
}

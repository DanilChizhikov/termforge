using System.Net;

namespace DTech.Core.NetworkDiagnostics;

public static class NetworkDiagnosticPlanner
{
    private static readonly HashSet<string> SupportedProtocols = ["dns", "tcp", "udp", "ssh", "tls", "http", "route"];

    public static NetworkDiagnosticPlan Create(NetworkDiagnosticRequest request)
    {
        var host = request.Host?.Trim();
        Uri? url = null;
        if (!string.IsNullOrWhiteSpace(request.Url))
        {
            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out url) || url.Scheme is not ("http" or "https") || url.UserInfo.Length > 0)
                throw new ArgumentException("Use an HTTP or HTTPS URL without embedded credentials.");
            host ??= url.IdnHost;
        }
        if (!ValidHost(host)) throw new ArgumentException("A valid IP address or hostname is required.");
        var domain = request.Domain?.Trim() ?? host!;
        if (!ValidHost(domain)) throw new ArgumentException("A valid DNS name is required.");
        if (request.Protocols is null || request.Protocols.Count == 0 || request.Protocols.Any(protocol => !SupportedProtocols.Contains(protocol)))
            throw new ArgumentException("Select DNS, TCP, UDP, SSH, TLS, HTTP or route checks.");
        if (request.Points is null || request.Points.Count is < 1 or > 8
            || request.Points.Any(point => point is null || point.Category is not ("rf" or "outsideRf") || point.ConnectionId == Guid.Empty)
            || request.Points.Select(point => point.ConnectionId).Distinct().Count() != request.Points.Count)
            throw new ArgumentException("Select 1 to 8 distinct points and label each rf or outsideRf.");

        var ports = ParsePorts(request);
        return new NetworkDiagnosticPlan
        {
            Host = host!, Domain = domain, Url = url, Ports = ports,
            Protocols = request.Protocols.Distinct().Order(StringComparer.Ordinal).ToArray(),
            Points = request.Points.OrderBy(point => point.ConnectionId).ToArray(),
        };
    }

    public static IEnumerable<NetworkProbe> Probes(NetworkDiagnosticPlan plan)
    {
        for (var i = 0; i < plan.Protocols.Count; i++)
        {
            var protocol = plan.Protocols[i];
            if (protocol is "dns" or "route" || protocol == "http" && plan.Url is not null)
            {
                yield return new NetworkProbe { Host = plan.Host, Domain = plan.Domain, Protocol = protocol, Url = plan.Url, Port = protocol == "http" ? plan.Url?.Port : null };
                continue;
            }
            for (var p = 0; p < plan.Ports.Count; p++)
            {
                yield return new NetworkProbe { Host = plan.Host, Domain = plan.Domain, Protocol = protocol, Port = plan.Ports[p] };
            }
        }
    }

    private static bool ValidHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Length > 253 || host.Any(char.IsControl) || host.StartsWith('-')) return false;
        return IPAddress.TryParse(host, out _) || Uri.CheckHostName(host) == UriHostNameType.Dns;
    }

    private static int[] ParsePorts(NetworkDiagnosticRequest request)
    {
        if (request.AllPorts) return Enumerable.Range(1, 65535).ToArray();
        var text = request.Ports ?? "22,80,443";
        if (text.Length > 4096) throw new ArgumentException("The port selection is too long.");
        var ports = new SortedSet<int>();
        var selections = text.Split(',', StringSplitOptions.TrimEntries);
        for (var i = 0; i < selections.Length; i++)
        {
            var range = selections[i].Split('-', StringSplitOptions.TrimEntries);
            if (range.Length is < 1 or > 2 || !int.TryParse(range[0], out var first) || first is < 1 or > 65535)
                throw new ArgumentException("Ports must be between 1 and 65535.");
            var last = first;
            if (range.Length == 2 && (!int.TryParse(range[1], out last) || last < first || last > 65535))
                throw new ArgumentException("Invalid port range.");
            for (var port = first; port <= last; port++) ports.Add(port);
        }
        return ports.ToArray();
    }
}

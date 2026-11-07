namespace DTech.Core.NetworkDiagnostics;

public sealed record NetworkPackagePlan(string Manager, IReadOnlyList<string> Packages, string Command);

public static class NetworkPackageInstaller
{
    public const string Discover = "uname -s; for manager in apt-get dnf apk pacman zypper; do if command -v \"$manager\" >/dev/null 2>&1; then printf '%s\\n' \"$manager\"; fi; done; id -u";

    public static NetworkPackagePlan Create(string manager, IReadOnlyList<string> protocols)
    {
        if (protocols.Count == 0) throw new ArgumentException("Select the checks whose utilities should be installed.");
        var tools = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < protocols.Count; i++)
        {
            if (protocols[i] == "udpDetails") tools.Add("nmap");
            else tools.Add(RemoteNetworkCommands.Tool(protocols[i]));
            if (protocols[i] == "tls") tools.Add("timeout");
        }
        var packages = tools.Select(tool => Package(manager, tool)).Distinct().Order(StringComparer.Ordinal).ToArray();
        var selection = string.Join(' ', packages);
        var command = manager switch
        {
            "apt-get" => $"env DEBIAN_FRONTEND=noninteractive apt-get install --no-upgrade --no-install-recommends -y -- {selection}",
            "dnf" => $"dnf install --assumeyes --setopt=install_weak_deps=False -- {selection}",
            "apk" => $"apk add --no-cache -- {selection}",
            "pacman" => $"pacman -S --needed --noconfirm -- {selection}",
            "zypper" => $"zypper --non-interactive install --no-recommends -- {selection}",
            _ => throw new ArgumentException("Unsupported package manager. Install the required utilities manually using your distribution's official repositories."),
        };
        return new NetworkPackagePlan(manager, packages, command);
    }

    private static string Package(string manager, string tool)
    {
        return (manager, tool) switch
        {
            ("apt-get", "nc") or ("apk", "nc") or ("zypper", "nc") => "netcat-openbsd",
            ("dnf", "nc") => "nmap-ncat",
            ("pacman", "nc") => "openbsd-netcat",
            ("apt-get", "dig") => "dnsutils",
            ("dnf", "dig") or ("zypper", "dig") => "bind-utils",
            ("apk", "dig") => "bind-tools",
            ("pacman", "dig") => "bind",
            ("apt-get", "ssh-keyscan") => "openssh-client",
            ("dnf", "ssh-keyscan") => "openssh-clients",
            ("apk", "ssh-keyscan") => "openssh-client-default",
            ("pacman", "ssh-keyscan") or ("zypper", "ssh-keyscan") => "openssh",
            (_, "timeout") => "coreutils",
            (_, "curl" or "openssl" or "traceroute" or "nmap") => tool,
            _ => throw new ArgumentException("Unsupported utility or package manager."),
        };
    }
}

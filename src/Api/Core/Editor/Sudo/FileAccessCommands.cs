using System.Globalization;

namespace DTech.Core.Editor.Sudo;

public sealed class FileAccessCommands
{
    private const string ProbeScript =
        "exec </dev/null; owner=$(getent passwd \"$1\" 2>/dev/null | cut -d: -f1); if [ -z \"$owner\" ]; then owner=$(awk -F: -v id=\"$1\" '$3 == id { print $1; exit }' /etc/passwd 2>/dev/null); fi; if [ -w \"$2\" ] && [ -w \"$3\" ]; then writable=1; else writable=0; fi; printf '%s\\n%s\\n' \"$writable\" \"$owner\"";

    public string Probe(int ownerId, RemoteFileLocation location)
    {
        var owner = ownerId.ToString(CultureInfo.InvariantCulture);
        return $"sh -c {ShellQuoting.Quote(ProbeScript)} termforge-access {owner} {ShellQuoting.Quote(location.Path)} {ShellQuoting.Quote(location.Directory)}";
    }
}

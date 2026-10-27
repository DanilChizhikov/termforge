namespace DTech.Core.SshConfig;

public sealed record SshConfigParseResult(bool FileFound, IReadOnlyList<SshConfigHostEntry> Hosts, IReadOnlyList<SshConfigIssue> Issues);

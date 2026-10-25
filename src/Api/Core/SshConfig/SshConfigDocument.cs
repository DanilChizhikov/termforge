namespace DTech.Core.SshConfig;

public sealed record SshConfigDocument(bool RootFound, IReadOnlyList<SshConfigDirective> Directives, IReadOnlyList<SshConfigIssue> Issues);

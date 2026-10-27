using DTech.Core.SshConfig;

namespace DTech.Core.SavedConnections;

public sealed record SshConfigImportResult(string ConfigPath, int Created, int Updated, IReadOnlyList<SshConfigIssue> Issues);

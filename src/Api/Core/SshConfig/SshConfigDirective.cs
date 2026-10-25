namespace DTech.Core.SshConfig;

public sealed record SshConfigDirective(string FilePath, int Line, string Keyword, IReadOnlyList<string> Values);

using DTech.Core.Connections;

namespace DTech.Core.Diagnostics;

public sealed record CapabilityProbeContext(SshConnectionTarget Target, IRemoteCommandRunner Runner);

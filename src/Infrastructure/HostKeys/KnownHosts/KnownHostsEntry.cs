using DTech.Core.Connections;

namespace DTech.HostKeys.KnownHosts;

internal sealed record KnownHostsEntry(KnownHostsMarker Marker, string HostPatterns, HostKey HostKey);

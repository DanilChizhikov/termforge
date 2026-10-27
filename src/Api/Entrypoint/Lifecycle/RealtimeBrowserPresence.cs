using DTech.Entrypoint.Realtime;

namespace DTech.Entrypoint.Lifecycle;

internal sealed class RealtimeBrowserPresence(RealtimeChannel channel) : IBrowserPresence
{
    public bool IsAnyBrowserConnected => channel.ConnectionCount > 0;
}

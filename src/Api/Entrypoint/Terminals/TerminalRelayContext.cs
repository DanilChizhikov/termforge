using System.Net.WebSockets;
using DTech.Core.Terminal.Sessions;

namespace DTech.Entrypoint.Terminals;

internal sealed record TerminalRelayContext(WebSocket WebSocket, ManagedTerminalSession Session, TerminalSocketWriter Writer);

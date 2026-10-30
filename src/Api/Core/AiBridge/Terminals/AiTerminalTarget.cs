using DTech.Core.SavedConnections;
using DTech.Core.Terminal.Sessions;

namespace DTech.Core.AiBridge.Terminals;

public sealed record AiTerminalTarget(ManagedTerminalSession Session, SavedConnection Connection);

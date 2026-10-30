using DTech.Core.Terminal.Sessions;

namespace DTech.Core.AiBridge.Terminals;

public sealed record AiTerminalOutput(TerminalSessionDescriptor Terminal, string Text, bool Truncated);

namespace DTech.Entrypoint.Terminals;

internal sealed record OpenTerminalRequest(Guid? ConnectionId, int Columns, int Rows);

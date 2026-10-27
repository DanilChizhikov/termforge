namespace DTech.Core.Terminal;

public readonly record struct TerminalSize(int Columns, int Rows)
{
    public bool IsValid => Columns > 0 && Rows > 0;
}

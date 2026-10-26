namespace DTech.Core.Terminal.Sessions;

public interface ITerminalOutputObserver
{
    void Observe(ReadOnlySpan<byte> output);
}

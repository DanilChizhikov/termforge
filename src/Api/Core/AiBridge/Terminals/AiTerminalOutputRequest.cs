namespace DTech.Core.AiBridge.Terminals;

public readonly record struct AiTerminalOutputRequest(string? TerminalId, int? MaxCharacters)
{
    public const int DefaultMaxCharacters = 8000;
    public const int MaxAllowedCharacters = 64 * 1024;

    public int BoundedMaxCharacters => Math.Clamp(MaxCharacters ?? DefaultMaxCharacters, 1, MaxAllowedCharacters);
}

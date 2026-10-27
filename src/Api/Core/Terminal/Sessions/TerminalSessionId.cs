using System.Security.Cryptography;

namespace DTech.Core.Terminal.Sessions;

public readonly record struct TerminalSessionId(string Value)
{
    private const int RandomByteCount = 32;

    public static TerminalSessionId Create()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(RandomByteCount);
        var value = Convert.ToHexStringLower(randomBytes);
        return new TerminalSessionId(value);
    }

    public override string ToString()
    {
        return Value;
    }
}

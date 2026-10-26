using System.Diagnostics.CodeAnalysis;

namespace DTech.Core.Terminal.Sessions;

public sealed class TerminalResult<T>
{
    private readonly T? _value;

    private TerminalResult(T? value, TerminalError? error)
    {
        _value = value;
        Error = error;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public TerminalError? Error { get; }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"The result has no value because it failed with {Error.Code}.");

    public static TerminalResult<T> Success(T value)
    {
        return new TerminalResult<T>(value, null);
    }

    public static TerminalResult<T> Failure(TerminalError error)
    {
        return new TerminalResult<T>(default, error);
    }
}

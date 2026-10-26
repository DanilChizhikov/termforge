using System.Diagnostics.CodeAnalysis;

namespace DTech.Core.Terminal.Recording;

public sealed class TerminalRecordingResult<T>
{
    private readonly T? _value;

    private TerminalRecordingResult(T? value, TerminalRecordingError? error)
    {
        _value = value;
        Error = error;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public TerminalRecordingError? Error { get; }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"The result has no value because it failed with {Error.Code}.");

    public static TerminalRecordingResult<T> Success(T value)
    {
        return new TerminalRecordingResult<T>(value, null);
    }

    public static TerminalRecordingResult<T> Failure(TerminalRecordingError error)
    {
        return new TerminalRecordingResult<T>(default, error);
    }
}

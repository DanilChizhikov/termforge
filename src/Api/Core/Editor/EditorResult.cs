using System.Diagnostics.CodeAnalysis;

namespace DTech.Core.Editor;

public sealed class EditorResult<T>
{
    private readonly T? _value;

    private EditorResult(T? value, EditorError? error)
    {
        _value = value;
        Error = error;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public EditorError? Error { get; }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"The result has no value because it failed with {Error.Code}.");

    public static EditorResult<T> Success(T value)
    {
        return new EditorResult<T>(value, null);
    }

    public static EditorResult<T> Failure(EditorError error)
    {
        return new EditorResult<T>(default, error);
    }
}

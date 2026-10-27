using System.Diagnostics.CodeAnalysis;

namespace DTech.Core.SettingsTransfer;

public sealed class SettingsTransferResult<T>
{
    private readonly T? _value;

    private SettingsTransferResult(T? value, SettingsTransferError? error)
    {
        _value = value;
        Error = error;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public SettingsTransferError? Error { get; }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"The result has no value because it failed with {Error.Code}.");

    public static SettingsTransferResult<T> Success(T value)
    {
        return new SettingsTransferResult<T>(value, null);
    }

    public static SettingsTransferResult<T> Failure(SettingsTransferError error)
    {
        return new SettingsTransferResult<T>(default, error);
    }
}

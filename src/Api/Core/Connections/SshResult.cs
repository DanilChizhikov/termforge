namespace DTech.Core.Connections;

public readonly record struct SshResult
{
    private SshResult(SshError? error)
    {
        Error = error;
    }

    public SshError? Error { get; }

    public bool IsSuccess => Error is null;

    public static SshResult Success()
    {
        return new SshResult(null);
    }

    public static SshResult Failure(SshError error)
    {
        return new SshResult(error);
    }
}

public readonly record struct SshResult<TValue>
{
    private readonly TValue? _value;

    private SshResult(TValue? value, SshError? error)
    {
        _value = value;
        Error = error;
    }

    public SshError? Error { get; }

    public bool IsSuccess => Error is null;

    public TValue Value => IsSuccess ? _value! : throw new InvalidOperationException($"Result has no value: {Error!.Kind} {Error.Message}");

    public static SshResult<TValue> Success(TValue value)
    {
        return new SshResult<TValue>(value, null);
    }

    public static SshResult<TValue> Failure(SshError error)
    {
        return new SshResult<TValue>(default, error);
    }
}

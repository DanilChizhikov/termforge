namespace DTech.Entrypoint.Security;

internal sealed class BootstrapToken
{
    private readonly Lock _consumeLock = new();
    private bool _isConsumed;

    public BootstrapToken()
    {
        Value = RandomSecret.Create();
    }

    public string Value { get; }

    public bool TryConsume(string? candidate)
    {
        lock (_consumeLock)
        {
            if (_isConsumed)
            {
                return false;
            }

            if (!SecretComparer.AreEqual(Value, candidate))
            {
                return false;
            }

            _isConsumed = true;
            return true;
        }
    }
}

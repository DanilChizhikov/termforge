namespace DTech.Credentials.Vault;

public sealed class VaultEntry(string key, byte[] value)
{
    public string Key { get; } = key;

    public byte[] Value { get; } = value;

    public override string ToString()
    {
        return Key;
    }
}

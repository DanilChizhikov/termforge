using System.Security.Cryptography;

namespace DTech.Credentials.Vault;

public sealed class VaultEntries : IDisposable
{
    private readonly List<VaultEntry> _entries = [];

    public IReadOnlyList<VaultEntry> Items => _entries;

    public VaultEntry? Find(string key)
    {
        var index = IndexOf(key);
        return index < 0 ? null : _entries[index];
    }

    public void Set(string key, ReadOnlySpan<byte> value)
    {
        Remove(key);
        var valueCopy = value.ToArray();
        var entry = new VaultEntry(key, valueCopy);
        _entries.Add(entry);
    }

    public bool Remove(string key)
    {
        var index = IndexOf(key);
        if (index < 0)
        {
            return false;
        }

        CryptographicOperations.ZeroMemory(_entries[index].Value);
        _entries.RemoveAt(index);
        return true;
    }

    public void Dispose()
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            CryptographicOperations.ZeroMemory(_entries[i].Value);
        }

        _entries.Clear();
    }

    private int IndexOf(string key)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            if (string.Equals(_entries[i].Key, key, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}

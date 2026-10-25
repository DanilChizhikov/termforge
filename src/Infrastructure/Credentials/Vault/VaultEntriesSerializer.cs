using System.Buffers.Binary;
using System.Text;
using DTech.Core.Credentials;

namespace DTech.Credentials.Vault;

public static class VaultEntriesSerializer
{
    private const int CountLength = sizeof(int);
    private const int KeyLengthLength = sizeof(ushort);
    private const int ValueLengthLength = sizeof(int);

    public static byte[] Serialize(VaultEntries entries)
    {
        var items = entries.Items;
        var totalLength = CountLength;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            totalLength += KeyLengthLength + Encoding.UTF8.GetByteCount(item.Key) + ValueLengthLength + item.Value.Length;
        }

        var buffer = new byte[totalLength];
        var span = buffer.AsSpan();
        BinaryPrimitives.WriteInt32BigEndian(span, items.Count);
        var position = CountLength;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var keyTarget = span[(position + KeyLengthLength)..];
            var keyLength = Encoding.UTF8.GetBytes(item.Key, keyTarget);
            BinaryPrimitives.WriteUInt16BigEndian(span[position..], (ushort)keyLength);
            position += KeyLengthLength + keyLength;
            BinaryPrimitives.WriteInt32BigEndian(span[position..], item.Value.Length);
            position += ValueLengthLength;
            item.Value.CopyTo(span[position..]);
            position += item.Value.Length;
        }

        return buffer;
    }

    public static CredentialResult<VaultEntries> Deserialize(ReadOnlySpan<byte> payload)
    {
        var entries = new VaultEntries();
        if (payload.Length < CountLength)
        {
            return Malformed(entries);
        }

        var count = BinaryPrimitives.ReadInt32BigEndian(payload);
        var position = CountLength;
        for (var i = 0; i < count; i++)
        {
            if (payload.Length - position < KeyLengthLength)
            {
                return Malformed(entries);
            }

            var keyLength = BinaryPrimitives.ReadUInt16BigEndian(payload[position..]);
            position += KeyLengthLength;
            if (payload.Length - position < keyLength + ValueLengthLength)
            {
                return Malformed(entries);
            }

            var keyBytes = payload.Slice(position, keyLength);
            var key = Encoding.UTF8.GetString(keyBytes);
            position += keyLength;
            var valueLength = BinaryPrimitives.ReadInt32BigEndian(payload[position..]);
            position += ValueLengthLength;
            if (valueLength < 0 || payload.Length - position < valueLength)
            {
                return Malformed(entries);
            }

            var value = payload.Slice(position, valueLength);
            entries.Set(key, value);
            position += valueLength;
        }

        if (position != payload.Length)
        {
            return Malformed(entries);
        }

        return CredentialResult<VaultEntries>.Success(entries);
    }

    private static CredentialResult<VaultEntries> Malformed(VaultEntries entries)
    {
        entries.Dispose();
        var error = CredentialErrors.VaultCorrupted("the decrypted content is malformed.");
        return CredentialResult<VaultEntries>.Failure(error);
    }
}

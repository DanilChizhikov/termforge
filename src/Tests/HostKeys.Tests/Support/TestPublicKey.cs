using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using DTech.Core.Connections;

namespace DTech.HostKeys.Tests.Support;

internal sealed record TestPublicKey(string Algorithm, string EncodedBlob)
{
    private const int KeyMaterialLength = 32;

    public HostKey HostKey
    {
        get
        {
            var blob = Convert.FromBase64String(EncodedBlob);
            return HostKey.FromPublicKeyBlob(blob);
        }
    }

    public static TestPublicKey CreateEd25519()
    {
        return Create("ssh-ed25519");
    }

    public static TestPublicKey CreateRsa()
    {
        return Create("ssh-rsa");
    }

    public string ToKnownHostsLine(string hostPatterns)
    {
        return $"{hostPatterns} {Algorithm} {EncodedBlob}";
    }

    private static TestPublicKey Create(string algorithm)
    {
        var algorithmBytes = Encoding.ASCII.GetBytes(algorithm);
        var keyMaterial = RandomNumberGenerator.GetBytes(KeyMaterialLength);
        var blob = new byte[4 + algorithmBytes.Length + 4 + keyMaterial.Length];
        BinaryPrimitives.WriteUInt32BigEndian(blob, (uint)algorithmBytes.Length);
        algorithmBytes.CopyTo(blob, 4);
        var keyOffset = 4 + algorithmBytes.Length;
        BinaryPrimitives.WriteUInt32BigEndian(blob.AsSpan(keyOffset), (uint)keyMaterial.Length);
        keyMaterial.CopyTo(blob, keyOffset + 4);
        var encodedBlob = Convert.ToBase64String(blob);
        return new TestPublicKey(algorithm, encodedBlob);
    }
}

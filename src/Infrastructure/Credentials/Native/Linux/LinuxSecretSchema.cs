using System.Runtime.InteropServices;

namespace DTech.Credentials.Native.Linux;

internal sealed unsafe class LinuxSecretSchema
{
    public const string Name = "com.dtech.termforge.Credential";
    public const string KeyAttribute = "key";

    private static readonly Lazy<LinuxSecretSchema> SharedInstance = new(Allocate);

    private LinuxSecretSchema(LibSecret.Schema* pointer)
    {
        Pointer = pointer;
    }

    public static LinuxSecretSchema Instance => SharedInstance.Value;

    public LibSecret.Schema* Pointer { get; }

    private static LinuxSecretSchema Allocate()
    {
        var size = (nuint)sizeof(LibSecret.Schema);
        var schema = (LibSecret.Schema*)NativeMemory.AllocZeroed(size);
        schema->Name = Marshal.StringToCoTaskMemUTF8(Name);
        schema->Flags = LibSecret.SchemaFlagsNone;
        schema->Attributes[0].Name = Marshal.StringToCoTaskMemUTF8(KeyAttribute);
        schema->Attributes[0].Type = LibSecret.SchemaAttributeString;
        return new LinuxSecretSchema(schema);
    }
}

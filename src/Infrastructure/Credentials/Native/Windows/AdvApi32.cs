using System.Runtime.InteropServices;

namespace DTech.Credentials.Native.Windows;

internal static unsafe partial class AdvApi32
{
    public const uint CredentialTypeGeneric = 1;
    public const uint CredentialPersistLocalMachine = 2;
    public const int MaxCredentialBlobSize = 5 * 512;
    public const int ErrorNotFound = 1168;
    public const int ErrorNoSuchLogonSession = 1312;

    private const string LibraryName = "advapi32.dll";

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public char* TargetName;
        public char* Comment;
        public uint LastWrittenLow;
        public uint LastWrittenHigh;
        public uint CredentialBlobSize;
        public byte* CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public char* TargetAlias;
        public char* UserName;
    }

    [LibraryImport(LibraryName, EntryPoint = "CredWriteW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CredWrite(NativeCredential* credential, uint flags);

    [LibraryImport(LibraryName, EntryPoint = "CredReadW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CredRead(string targetName, uint type, uint flags, out NativeCredential* credential);

    [LibraryImport(LibraryName, EntryPoint = "CredDeleteW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CredDelete(string targetName, uint type, uint flags);

    [LibraryImport(LibraryName, EntryPoint = "CredFree")]
    public static partial void CredFree(void* buffer);
}

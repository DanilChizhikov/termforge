using System.Runtime.InteropServices;

namespace DTech.Credentials.Native.Linux;

internal static unsafe partial class GLib
{
    public const string LibraryName = "libglib-2.0.so.0";

    [StructLayout(LayoutKind.Sequential)]
    public struct Error
    {
        public uint Domain;
        public int Code;
        public byte* Message;
    }

    [LibraryImport(LibraryName, EntryPoint = "g_hash_table_new")]
    public static partial IntPtr HashTableNew(IntPtr hashFunction, IntPtr equalFunction);

    [LibraryImport(LibraryName, EntryPoint = "g_hash_table_insert")]
    public static partial int HashTableInsert(IntPtr table, IntPtr key, IntPtr value);

    [LibraryImport(LibraryName, EntryPoint = "g_hash_table_unref")]
    public static partial void HashTableUnref(IntPtr table);

    [LibraryImport(LibraryName, EntryPoint = "g_error_free")]
    public static partial void ErrorFree(Error* error);
}

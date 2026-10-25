using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DTech.Credentials.Native.Linux;

internal static unsafe partial class LibSecret
{
    public const string LibraryName = "libsecret-1.so.0";
    public const int SchemaFlagsNone = 0;
    public const int SchemaAttributeString = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct SchemaAttribute
    {
        public IntPtr Name;
        public int Type;
    }

    [InlineArray(32)]
    public struct SchemaAttributes
    {
        private SchemaAttribute _element;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Schema
    {
        public IntPtr Name;
        public int Flags;
        public SchemaAttributes Attributes;
        public int Reserved;
        public IntPtr Reserved1;
        public IntPtr Reserved2;
        public IntPtr Reserved3;
        public IntPtr Reserved4;
        public IntPtr Reserved5;
        public IntPtr Reserved6;
        public IntPtr Reserved7;
    }

    [LibraryImport(LibraryName, EntryPoint = "secret_password_storev_sync")]
    public static partial int StorePassword(Schema* schema, IntPtr attributes, byte* collection, byte* label, byte* password, IntPtr cancellable, GLib.Error** error);

    [LibraryImport(LibraryName, EntryPoint = "secret_password_lookupv_sync")]
    public static partial byte* LookupPassword(Schema* schema, IntPtr attributes, IntPtr cancellable, GLib.Error** error);

    [LibraryImport(LibraryName, EntryPoint = "secret_password_clearv_sync")]
    public static partial int ClearPassword(Schema* schema, IntPtr attributes, IntPtr cancellable, GLib.Error** error);

    [LibraryImport(LibraryName, EntryPoint = "secret_password_free")]
    public static partial void FreePassword(byte* password);
}

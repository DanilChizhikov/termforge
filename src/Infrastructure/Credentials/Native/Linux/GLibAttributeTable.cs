using System.Runtime.InteropServices;

namespace DTech.Credentials.Native.Linux;

internal sealed class GLibAttributeTable : IDisposable
{
    private static readonly Lazy<GLibStringFunctions> StringFunctions = new(GLibStringFunctions.Load);

    private readonly IntPtr _name;
    private readonly IntPtr _value;

    public GLibAttributeTable(string name, string value)
    {
        var functions = StringFunctions.Value;
        _name = Marshal.StringToCoTaskMemUTF8(name);
        _value = Marshal.StringToCoTaskMemUTF8(value);
        Handle = GLib.HashTableNew(functions.Hash, functions.Equal);
        GLib.HashTableInsert(Handle, _name, _value);
    }

    public IntPtr Handle { get; }

    public void Dispose()
    {
        GLib.HashTableUnref(Handle);
        Marshal.FreeCoTaskMem(_name);
        Marshal.FreeCoTaskMem(_value);
    }

    private sealed record GLibStringFunctions(IntPtr Hash, IntPtr Equal)
    {
        public static GLibStringFunctions Load()
        {
            var library = NativeLibrary.Load(GLib.LibraryName);
            var hash = NativeLibrary.GetExport(library, "g_str_hash");
            var equal = NativeLibrary.GetExport(library, "g_str_equal");
            return new GLibStringFunctions(hash, equal);
        }
    }
}

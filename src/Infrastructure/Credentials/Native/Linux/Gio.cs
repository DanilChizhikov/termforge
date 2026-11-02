using System.Runtime.InteropServices;

namespace DTech.Credentials.Native.Linux;

internal static partial class Gio
{
    public const string LibraryName = "libgio-2.0.so.0";

    [LibraryImport(LibraryName, EntryPoint = "g_cancellable_new")]
    public static partial IntPtr CancellableNew();

    [LibraryImport(LibraryName, EntryPoint = "g_cancellable_cancel")]
    public static partial void CancellableCancel(IntPtr cancellable);

    [LibraryImport(LibraryName, EntryPoint = "g_cancellable_is_cancelled")]
    public static partial int CancellableIsCancelled(IntPtr cancellable);

    [LibraryImport("libgobject-2.0.so.0", EntryPoint = "g_object_unref")]
    public static partial void ObjectUnref(IntPtr instance);
}

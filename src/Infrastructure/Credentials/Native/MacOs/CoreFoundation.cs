using System.Runtime.InteropServices;

namespace DTech.Credentials.Native.MacOs;

internal static unsafe partial class CoreFoundation
{
    public const string LibraryPath = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    public const uint StringEncodingUtf8 = 0x08000100;

    [LibraryImport(LibraryPath)]
    public static partial void CFRelease(IntPtr handle);

    [LibraryImport(LibraryPath, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr CFStringCreateWithCString(IntPtr allocator, string value, uint encoding);

    [LibraryImport(LibraryPath)]
    public static partial nint CFStringGetLength(IntPtr value);

    [LibraryImport(LibraryPath)]
    public static partial nint CFStringGetMaximumSizeForEncoding(nint length, uint encoding);

    [LibraryImport(LibraryPath)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool CFStringGetCString(IntPtr value, byte* buffer, nint bufferSize, uint encoding);

    [LibraryImport(LibraryPath)]
    public static partial IntPtr CFDataCreate(IntPtr allocator, byte* bytes, nint length);

    [LibraryImport(LibraryPath)]
    public static partial byte* CFDataGetBytePtr(IntPtr data);

    [LibraryImport(LibraryPath)]
    public static partial nint CFDataGetLength(IntPtr data);

    [LibraryImport(LibraryPath)]
    public static partial IntPtr CFDictionaryCreate(IntPtr allocator, IntPtr* keys, IntPtr* values, nint count, IntPtr keyCallBacks, IntPtr valueCallBacks);
}

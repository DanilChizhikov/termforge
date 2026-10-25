namespace DTech.Credentials.Native.MacOs;

internal sealed unsafe class CoreFoundationDictionaryBuilder(MacOsSecurityConstants constants) : IDisposable
{
    private readonly List<IntPtr> _keys = [];
    private readonly List<IntPtr> _values = [];
    private readonly List<IntPtr> _ownedObjects = [];

    public void Add(IntPtr key, IntPtr value)
    {
        _keys.Add(key);
        _values.Add(value);
    }

    public void AddString(IntPtr key, string value)
    {
        var text = CoreFoundation.CFStringCreateWithCString(IntPtr.Zero, value, CoreFoundation.StringEncodingUtf8);
        Track(text);
        Add(key, text);
    }

    public void AddData(IntPtr key, ReadOnlySpan<byte> value)
    {
        IntPtr data;
        fixed (byte* bytes = value)
        {
            data = CoreFoundation.CFDataCreate(IntPtr.Zero, bytes, value.Length);
        }

        Track(data);
        Add(key, data);
    }

    public IntPtr Build()
    {
        var keys = _keys.ToArray();
        var values = _values.ToArray();
        IntPtr dictionary;
        fixed (IntPtr* keyPointer = keys)
        fixed (IntPtr* valuePointer = values)
        {
            dictionary = CoreFoundation.CFDictionaryCreate(IntPtr.Zero, keyPointer, valuePointer, keys.Length, constants.DictionaryKeyCallBacks, constants.DictionaryValueCallBacks);
        }

        Track(dictionary);
        return dictionary;
    }

    public void Dispose()
    {
        for (var i = 0; i < _ownedObjects.Count; i++)
        {
            CoreFoundation.CFRelease(_ownedObjects[i]);
        }

        _ownedObjects.Clear();
    }

    private void Track(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("CoreFoundation could not allocate an object.");
        }

        _ownedObjects.Add(handle);
    }
}

namespace DTech.Credentials.Native.Linux;

internal sealed unsafe class GLibCancellation : IDisposable
{
    private readonly CancellationTokenSource _deadline;
    private readonly CancellationTokenSource _linked;
    private readonly CancellationTokenRegistration _registration;
    private IntPtr _handle;

    public GLibCancellation(CancellationToken cancellationToken, TimeSpan timeout)
    {
        _deadline = new CancellationTokenSource(timeout);
        _linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _deadline.Token);
        _handle = Gio.CancellableNew();
        if (_handle == IntPtr.Zero)
        {
            _linked.Dispose();
            _deadline.Dispose();
            throw new InvalidOperationException("GIO could not create a cancellation object.");
        }

        _registration = _linked.Token.Register(static state => Gio.CancellableCancel((IntPtr)state!), _handle);
    }

    public IntPtr Handle => _handle;

    public void ThrowIfCancellationRequested(GLib.Error* error)
    {
        if (_linked.IsCancellationRequested)
        {
            if (error is not null)
            {
                GLib.ErrorFree(error);
            }

            _linked.Token.ThrowIfCancellationRequested();
        }
    }

    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (handle != IntPtr.Zero)
        {
            _registration.Dispose();
            _linked.Dispose();
            _deadline.Dispose();
            Gio.ObjectUnref(handle);
        }
    }
}

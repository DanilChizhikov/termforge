namespace DTech.Entrypoint.Lifecycle;

internal sealed class WorkAdmissionGate
{
    private readonly Lock _gate = new();
    private ApplicationExitState _state = ApplicationExitState.Running;

    public ApplicationExitState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public bool IsAcceptingWork => State == ApplicationExitState.Running;

    public bool TryBeginWaiting()
    {
        lock (_gate)
        {
            if (_state != ApplicationExitState.Running)
            {
                return false;
            }

            _state = ApplicationExitState.WaitingForTransfers;
            return true;
        }
    }

    public bool TryBeginShutdown()
    {
        lock (_gate)
        {
            if (_state == ApplicationExitState.ShuttingDown)
            {
                return false;
            }

            _state = ApplicationExitState.ShuttingDown;
            return true;
        }
    }

    public bool TryReopen()
    {
        lock (_gate)
        {
            if (_state != ApplicationExitState.WaitingForTransfers)
            {
                return false;
            }

            _state = ApplicationExitState.Running;
            return true;
        }
    }

    public void Close()
    {
        lock (_gate)
        {
            _state = ApplicationExitState.ShuttingDown;
        }
    }
}

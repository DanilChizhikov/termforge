using DTech.Core.Connections;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace DTech.Ssh.Connections;

internal sealed class SshConnectionHealth : IDisposable
{
    private readonly BaseClient _client;
    private readonly SshConnectionHealthOptions _options;
    private readonly CancellationTokenSource _stopping = new();
    private readonly CancellationTokenSource _closed = new();
    private readonly TaskCompletionSource _failed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _monitor;
    private SshError? _failure;
    private int _clientDisposed;
    private int _disposed;

    public SshConnectionHealth(BaseClient client, SshConnectionHealthOptions options)
    {
        _client = client;
        _options = options;
        Closed = _closed.Token;
        client.ErrorOccurred += OnErrorOccurred;
        _monitor = MonitorAsync();
    }

    public CancellationToken Closed { get; }

    public SshError? Failure => Volatile.Read(ref _failure);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _client.ErrorOccurred -= OnErrorOccurred;
        try
        {
            _stopping.Cancel();
            _closed.Cancel();
        }
        finally
        {
            try
            {
                DisposeClient();
            }
            finally
            {
                try
                {
                    _monitor.GetAwaiter().GetResult();
                }
                finally
                {
                    _stopping.Dispose();
                    _closed.Dispose();
                }
            }
        }
    }

    private async Task MonitorAsync()
    {
        Task? probe = null;
        try
        {
            if (!_options.ProbeEnabled)
            {
                await _failed.Task.WaitAsync(_stopping.Token);
                return;
            }

            while (!_stopping.IsCancellationRequested && Failure is null)
            {
                var delay = Task.Delay(_options.Interval, _stopping.Token);
                var next = await Task.WhenAny(delay, _failed.Task);
                if (next == _failed.Task)
                {
                    break;
                }

                await delay;
                probe = Task.Run(() => ProbeAsync(_stopping.Token));
                await probe.WaitAsync(_options.Timeout, _stopping.Token);
                probe = null;
            }
        }
        catch (Exception) when (_stopping.IsCancellationRequested)
        {
        }
        catch (TimeoutException)
        {
            var timeout = new TimeoutException($"The SSH connection did not respond within {_options.Timeout.TotalSeconds} seconds.");
            SetFailure(timeout);
        }
        catch (Exception exception)
        {
            SetFailure(exception);
        }
        finally
        {
            try
            {
                if (Failure is not null)
                {
                    _closed.Cancel();
                    DisposeClient();
                }
            }
            finally
            {
                if (probe is not null)
                {
                    await ObserveProbeAsync(probe);
                }
            }
        }
    }

    private Task ProbeAsync(CancellationToken cancellationToken)
    {
        return _client switch
        {
            SshClient ssh => ProbeShellAsync(ssh, cancellationToken),
            SftpClient sftp => sftp.GetAttributesAsync(".", cancellationToken),
            _ => throw new NotSupportedException("The SSH client has no connection health probe."),
        };
    }

    private async Task ProbeShellAsync(SshClient client, CancellationToken cancellationToken)
    {
        using var command = client.CreateCommand("true");
        command.CommandTimeout = _options.Timeout;
        await command.ExecuteAsync(cancellationToken);
        if (command.ExitStatus != 0)
        {
            throw new IOException("The SSH server rejected the connection health check (exec true).");
        }
    }

    private void OnErrorOccurred(object? sender, ExceptionEventArgs args)
    {
        SetFailure(args.Exception);
        _failed.TrySetResult();
    }

    private void SetFailure(Exception exception)
    {
        var error = new SshError(SshErrorKind.ConnectionFailed, $"SSH connection lost: {exception.Message}");
        Interlocked.CompareExchange(ref _failure, error, null);
    }

    private void DisposeClient()
    {
        if (Interlocked.Exchange(ref _clientDisposed, 1) == 0)
        {
            _client.Dispose();
        }
    }

    private static async Task ObserveProbeAsync(Task probe)
    {
        try
        {
            await probe;
        }
        catch (Exception)
        {
        }
    }
}

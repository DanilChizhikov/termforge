namespace DTech.Ssh.Connections;

internal static class SshResourceCleanup
{
    public static void Dispose(IDisposable? primary, IReadOnlyList<IDisposable> dependencies)
    {
        List<Exception>? failures = null;
        var primaryFailure = TryDispose(primary);
        if (primaryFailure is not null)
        {
            failures = [primaryFailure];
        }

        for (var i = 0; i < dependencies.Count; i++)
        {
            var failure = TryDispose(dependencies[i]);
            if (failure is not null)
            {
                failures ??= [];
                failures.Add(failure);
            }
        }

        if (failures is not null)
        {
            throw new AggregateException("SSH resources could not be completely released.", failures);
        }
    }

    private static Exception? TryDispose(IDisposable? resource)
    {
        try
        {
            resource?.Dispose();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }
}

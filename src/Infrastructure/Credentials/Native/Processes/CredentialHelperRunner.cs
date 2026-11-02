using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using DTech.Core.Credentials;
using Microsoft.Extensions.Options;

namespace DTech.Credentials.Native.Processes;

public sealed class CredentialHelperRunner(ICredentialHelperProcessFactory processes, IOptions<CredentialStoreOptions> options)
{
    private readonly SemaphoreSlim _gate = new(2, 2);

    public int OperationTimeoutMilliseconds => (int)Math.Ceiling(options.Value.OperationTimeout.TotalMilliseconds);

    public async Task<CredentialHelperPacket<CredentialHelperResponse>> RunAsync(CredentialHelperPacket<CredentialHelperRequest> request, CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(options.Value.OperationTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var entered = false;
        try
        {
            await _gate.WaitAsync(linked.Token);
            entered = true;
            return await ExchangeAsync(request, linked.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable("The operating system credential store did not respond before its deadline. A write or delete may have completed; its result is unknown.");
        }
        catch (Exception exception) when (exception is IOException or Win32Exception or InvalidOperationException or JsonException)
        {
            return Unavailable($"The operating system credential helper could not complete ({exception.GetType().Name}). A write or delete may have completed; its result is unknown.");
        }
        finally
        {
            if (entered)
            {
                _gate.Release();
            }
        }
    }

    private async Task<CredentialHelperPacket<CredentialHelperResponse>> ExchangeAsync(CredentialHelperPacket<CredentialHelperRequest> request, CancellationToken cancellationToken)
    {
        using var process = processes.Start();
        var stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null, cancellationToken);
        CredentialHelperPacket<CredentialHelperResponse>? response = null;
        var transferred = false;
        try
        {
            await CredentialHelperProtocol.WriteAsync(process.StandardInput.BaseStream, request, cancellationToken);
            process.StandardInput.Close();
            response = await CredentialHelperProtocol.ReadAsync<CredentialHelperResponse>(process.StandardOutput.BaseStream, cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("The credential helper exited unsuccessfully.");
            }

            transferred = true;
            return response;
        }
        finally
        {
            if (!transferred)
            {
                response?.Dispose();
            }

            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                await process.WaitForExitAsync(CancellationToken.None);
            }

            try
            {
                await stderr;
            }
            catch (Exception exception) when (exception is IOException or OperationCanceledException)
            {
            }
        }
    }

    private static CredentialHelperPacket<CredentialHelperResponse> Unavailable(string message)
    {
        var error = CredentialErrors.StoreUnavailable(message);
        var metadata = new CredentialHelperResponse(error);
        return new CredentialHelperPacket<CredentialHelperResponse>(metadata, []);
    }
}

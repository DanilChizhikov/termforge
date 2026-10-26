using System.Text.Json;
using System.Text.Json.Serialization;
using DTech.Core.Files.Search;
using Microsoft.AspNetCore.Http.Features;

namespace DTech.Entrypoint.Files.Search;

internal sealed class FileSearchStreamResult(IFileSearchExecution execution) : IResult
{
    public const string ContentType = "application/x-ndjson";

    private static readonly byte[] RecordTerminator = "\n"u8.ToArray();

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        await using (execution)
        {
            var response = httpContext.Response;
            response.StatusCode = StatusCodes.Status200OK;
            response.ContentType = ContentType;
            response.Headers.CacheControl = "no-store";
            httpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
            var cancellationToken = httpContext.RequestAborted;
            try
            {
                await WriteEventsAsync(response.Body, cancellationToken);
            }
            catch (Exception exception) when ((exception is OperationCanceledException or IOException) && cancellationToken.IsCancellationRequested)
            {
            }
        }
    }

    private async Task WriteEventsAsync(Stream body, CancellationToken cancellationToken)
    {
        await foreach (var searchEvent in execution.ExecuteAsync(cancellationToken))
        {
            var payload = FileSearchEventResponse.From(searchEvent);
            await JsonSerializer.SerializeAsync(body, payload, SerializerOptions, cancellationToken);
            await body.WriteAsync(RecordTerminator, cancellationToken);
            await body.FlushAsync(cancellationToken);
        }
    }
}

using DTech.Core.Terminal.Recording;
using DTech.Core.Terminal.Sessions;
using DTech.Entrypoint.Recordings;
using DTech.Entrypoint.Terminals;

namespace Microsoft.AspNetCore.Builder;

public static class TerminalRecordingEndpoints
{
    public const string RoutePrefix = "/api/recordings";
    public const string SessionRoute = "/api/terminals/{id}/recording";

    public static IEndpointRouteBuilder MapTerminalRecordingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var recordings = endpoints.MapGroup(RoutePrefix);
        recordings.MapGet("/settings", GetSettings);
        recordings.MapPut("/settings", UpdateSettings);
        recordings.MapGet("/", List);
        recordings.MapGet("/{id}/cast", Download);
        recordings.MapDelete("/{id}", Delete);

        var session = endpoints.MapGroup(SessionRoute);
        session.MapGet("/", GetSessionRecording);
        session.MapPost("/", StartSessionRecording);
        session.MapDelete("/", StopSessionRecording);
        return endpoints;
    }

    private static async Task<IResult> GetSettings(TerminalRecordingSettingsService settingsService, TerminalRecordingStorage storage, CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        var response = TerminalRecordingSettingsResponse.From(settings, storage);
        return Results.Ok(response);
    }

    private static async Task<IResult> UpdateSettings([AsParameters] RecordingSettingsCall call)
    {
        var requested = call.Body.ToSettings();
        var updated = await call.SettingsService.UpdateAsync(requested, call.CancellationToken);
        if (!updated.IsSuccess)
        {
            return TerminalRecordingErrorResults.ToProblem(updated.Error);
        }

        var response = TerminalRecordingSettingsResponse.From(updated.Value, call.Storage);
        return Results.Ok(response);
    }

    private static async Task<IResult> List(TerminalRecordingLibrary library, CancellationToken cancellationToken)
    {
        var summaries = await library.ListAsync(cancellationToken);
        var responses = new List<TerminalRecordingResponse>(summaries.Count);
        for (var i = 0; i < summaries.Count; i++)
        {
            var response = TerminalRecordingResponse.From(summaries[i]);
            responses.Add(response);
        }

        return Results.Ok(responses);
    }

    private static async Task<IResult> Download(string id, TerminalRecordingLibrary library, CancellationToken cancellationToken)
    {
        var opened = await library.OpenAsync(id, cancellationToken);
        if (!opened.IsSuccess)
        {
            return TerminalRecordingErrorResults.ToProblem(opened.Error);
        }

        return Results.Stream(opened.Value, AsciicastFormat.MediaType);
    }

    private static async Task<IResult> Delete(string id, TerminalRecordingLibrary library, CancellationToken cancellationToken)
    {
        var deleted = await library.DeleteAsync(id, cancellationToken);
        return deleted.IsSuccess ? Results.NoContent() : TerminalRecordingErrorResults.ToProblem(deleted.Error);
    }

    private static async Task<IResult> GetSessionRecording([AsParameters] SessionRecordingCall call)
    {
        var session = FindSession(call);
        if (session is null)
        {
            return CreateSessionNotFound();
        }

        var settings = await call.SettingsService.GetAsync(call.Context.RequestAborted);
        var recorder = call.RecordingService.FindLatest(session);
        var response = SessionRecordingResponse.From(recorder, settings.Enabled);
        return Results.Ok(response);
    }

    private static async Task<IResult> StartSessionRecording([AsParameters] SessionRecordingCall call)
    {
        var session = FindSession(call);
        if (session is null)
        {
            return CreateSessionNotFound();
        }

        var started = await call.RecordingService.StartAsync(session, call.Context.RequestAborted);
        if (!started.IsSuccess)
        {
            return TerminalRecordingErrorResults.ToProblem(started.Error);
        }

        var response = SessionRecordingResponse.From(started.Value, true);
        return Results.Ok(response);
    }

    private static async Task<IResult> StopSessionRecording([AsParameters] SessionRecordingCall call)
    {
        var session = FindSession(call);
        if (session is null)
        {
            return CreateSessionNotFound();
        }

        var stopped = await call.RecordingService.StopAsync(session);
        if (!stopped.IsSuccess)
        {
            return TerminalRecordingErrorResults.ToProblem(stopped.Error);
        }

        var settings = await call.SettingsService.GetAsync(call.Context.RequestAborted);
        var response = SessionRecordingResponse.From(stopped.Value, settings.Enabled);
        return Results.Ok(response);
    }

    private static ManagedTerminalSession? FindSession(SessionRecordingCall call)
    {
        var owner = TerminalOwner.From(call.Context);
        var sessionId = new TerminalSessionId(call.Id);
        return call.SessionManager.Find(sessionId, owner);
    }

    private static IResult CreateSessionNotFound()
    {
        var notFound = new TerminalError(TerminalErrorCode.NotFound, "The terminal session does not exist or belongs to another browser session.");
        return TerminalErrorResults.ToProblem(notFound);
    }
}

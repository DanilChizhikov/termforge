namespace DTech.Entrypoint.Realtime;

public sealed record RealtimeMessage<TPayload>(string Type, TPayload Data);

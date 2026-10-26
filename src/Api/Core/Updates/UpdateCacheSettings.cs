namespace DTech.Core.Updates;

public readonly record struct UpdateCacheSettings(TimeSpan SuccessDuration, TimeSpan FailureDuration);

namespace DTech.Core.Monitoring;

public readonly record struct LoadAverage(double OneMinute, double FiveMinutes, double FifteenMinutes);

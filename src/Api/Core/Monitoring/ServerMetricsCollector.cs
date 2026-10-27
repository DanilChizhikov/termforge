using DTech.Core.Connections;
using DTech.Core.Diagnostics;
using DTech.Core.Monitoring.Parsing;

namespace DTech.Core.Monitoring;

public sealed class ServerMetricsCollector
{
    public async Task<SshResult<MetricReadings>> CollectAsync(IRemoteCommandRunner runner, CancellationToken cancellationToken)
    {
        var result = await runner.RunScriptAsync(MetricsScript.Text, cancellationToken);
        if (!result.IsSuccess)
        {
            return SshResult<MetricReadings>.Failure(result.Error!);
        }

        var readings = MetricsOutputParser.Parse(result.Value.StandardOutput);
        return SshResult<MetricReadings>.Success(readings);
    }
}

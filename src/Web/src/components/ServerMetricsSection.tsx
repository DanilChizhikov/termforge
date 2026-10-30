import { resumeMetrics, useServerMetrics } from '../state/serverMetrics';
import { MetricCards } from './MetricCards';
import { RefreshArcIcon } from './icons/Icons';

export function ServerMetricsSection({ serverId }: { serverId: string }) {
  const snapshot = useServerMetrics(serverId);
  const { metrics } = snapshot;

  return (
    <section aria-label="Server metrics" className="metrics-section">
      {metrics === null && snapshot.error === null ? <p className="text-secondary">Collecting metrics…</p> : null}
      {snapshot.error !== null ? <p className="form-error">{snapshot.error}</p> : null}
      {snapshot.isPolling || snapshot.isLoading ? null : (
        <p role="status" className="notice metrics-paused">
          Monitoring paused · polling stopped
          <button type="button" className="btn btn-sm" onClick={() => resumeMetrics(serverId)}>
            <RefreshArcIcon size={13} />
            Measure now
          </button>
        </p>
      )}
      {metrics !== null ? <MetricCards metrics={metrics} trend={snapshot.trend} /> : null}
    </section>
  );
}

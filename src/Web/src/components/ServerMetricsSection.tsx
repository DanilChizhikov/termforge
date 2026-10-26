import { resumeMetrics, useServerMetrics } from '../state/serverMetrics';
import {
  MONITORING_INTERVALS_SECONDS,
  setMonitoringInterval,
  useMonitoringInterval,
} from '../state/monitoringInterval';
import { MetricCards } from './MetricCards';
import { RefreshIcon } from './icons/Icons';

export function ServerMetricsSection({ serverId }: { serverId: string }) {
  const snapshot = useServerMetrics(serverId);
  const intervalSeconds = useMonitoringInterval();
  const { metrics } = snapshot;

  return (
    <section aria-label="Server metrics" className="metrics-section">
      <div className="view-header">
        <div className="view-header-text">
          <h3 className="section-label">Monitoring</h3>
          <span className="view-subtitle">
            {metrics === null ? 'Collecting metrics...' : `Checked ${new Date(metrics.collectedAt).toLocaleTimeString()}`}
            {snapshot.isPolling ? ` · every ${intervalSeconds} s while open` : ' · polling stopped'}
          </span>
        </div>
        <label className="field metrics-interval">
          <span className="field-label">Interval</span>
          <select
            className="input input-sm"
            value={intervalSeconds}
            onChange={(event) => setMonitoringInterval(Number(event.target.value))}
          >
            {MONITORING_INTERVALS_SECONDS.map((seconds) => (
              <option key={seconds} value={seconds}>
                {seconds} s
              </option>
            ))}
          </select>
        </label>
        <button
          type="button"
          className="btn btn-sm"
          disabled={snapshot.isLoading}
          onClick={() => resumeMetrics(serverId)}
        >
          <RefreshIcon size={13} />
          Measure now
        </button>
      </div>
      {snapshot.error !== null ? <p className="form-error">{snapshot.error}</p> : null}
      {metrics !== null ? <MetricCards metrics={metrics} /> : null}
    </section>
  );
}

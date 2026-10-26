import type { Metric } from '../api/monitoring';
import { useServerMetrics } from '../state/serverMetrics';
import { formatBytes, formatUptime, percentOf, unitIndexOf } from './metricsFormat';

function describe<TValue>(metric: Metric<TValue>, format: (value: TValue) => string): string {
  if (!metric.available || metric.value === null) {
    return 'Not measured';
  }
  return format(metric.value);
}

function Row({ label, text, metric }: { label: string; text: string; metric: Metric<unknown> }) {
  return (
    <div className="sidebar-metrics-row">
      <span>{label}</span>
      <span
        className={metric.available ? 'sidebar-metrics-value' : 'sidebar-metrics-missing'}
        title={metric.reason ?? undefined}
      >
        {text}
      </span>
    </div>
  );
}

export function SidebarMetrics({ serverId }: { serverId: string }) {
  const { metrics, error } = useServerMetrics(serverId);
  if (metrics === null) {
    return error === null ? null : <p className="form-error">{error}</p>;
  }
  if (metrics.state !== 'Connected') {
    return (
      <section aria-label="Selected server metrics" className="sidebar-metrics">
        <span className="form-error">{metrics.message ?? metrics.state}</span>
      </section>
    );
  }

  const disk = metrics.disk;
  return (
    <section aria-label="Selected server metrics" className="sidebar-metrics">
      <Row label="Uptime" text={describe(metrics.uptimeSeconds, formatUptime)} metric={metrics.uptimeSeconds} />
      <Row label="CPU" text={describe(metrics.cpuPercent, (value) => `${value.toFixed(0)}%`)} metric={metrics.cpuPercent} />
      <Row
        label="Memory"
        text={describe(metrics.memory, (value) => `${percentOf(value.usedBytes, value.totalBytes)}% used`)}
        metric={metrics.memory}
      />
      <Row
        label="Disk"
        text={describe(disk, (value) => {
          const free = formatBytes(value.freeBytes, unitIndexOf(value.freeBytes));
          return `${free.amount} ${free.unit} free`;
        })}
        metric={disk}
      />
    </section>
  );
}

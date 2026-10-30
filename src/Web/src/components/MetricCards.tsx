import type { LoadAverage, ServerMetrics } from '../api/monitoring';
import type { MetricsTrend } from '../state/serverMetrics';
import { CpuSparkline } from './CpuSparkline';
import { formatBytes, formatUptime, percentOf, unitIndexOf, type ByteSize } from './metricsFormat';

export interface MetricCardsProps {
  metrics: ServerMetrics;
  trend: MetricsTrend;
}

interface MetricCardProps {
  metrics: ServerMetrics;
}

const NOT_MEASURED = 'Not measured';

export function MetricCards({ metrics, trend }: MetricCardsProps) {
  return (
    <div className="metric-grid">
      <ConnectionCard metrics={metrics} trend={trend} />
      <UptimeCard metrics={metrics} />
      <CpuCard metrics={metrics} trend={trend} />
      <MemoryCard metrics={metrics} />
      <DiskCard metrics={metrics} />
    </div>
  );
}

function ConnectionCard({ metrics, trend }: MetricCardsProps) {
  const connected = metrics.state === 'Connected';
  return (
    <div className="metric-card" role="group" aria-label="Connection">
      <span className="metric-label">Connection</span>
      <span className="metric-value">
        <span className={`status-dot ${connected ? 'status-dot-ok' : 'status-dot-danger'}`} aria-hidden="true" />
        {describeConnectionValue(metrics)}
      </span>
      <span className="metric-detail">
        {connected ? describeConnectedFor(metrics, trend) : (metrics.message ?? metrics.state)}
      </span>
    </div>
  );
}

function describeConnectionValue(metrics: ServerMetrics): string {
  if (metrics.state !== 'Connected') {
    return 'Disconnected';
  }
  return metrics.latencyMilliseconds === null ? 'Connected' : `${metrics.latencyMilliseconds} ms`;
}

function describeConnectedFor(metrics: ServerMetrics, trend: MetricsTrend): string {
  const drops = trend.dropsToday === 1 ? '1 drop today' : `${trend.dropsToday} drops today`;
  if (trend.connectedSince === null) {
    return `Connected · ${drops}`;
  }
  const connectedMilliseconds = Date.parse(metrics.collectedAt) - Date.parse(trend.connectedSince);
  const connectedSeconds = Math.max(0, connectedMilliseconds / 1000);
  return `Connected ${formatUptime(connectedSeconds)} · ${drops}`;
}

function formatLoadAverage(load: LoadAverage): string {
  return `Load ${load.oneMinute.toFixed(2)} · ${load.fiveMinutes.toFixed(2)} · ${load.fifteenMinutes.toFixed(2)}`;
}

function UnavailableCard({ label, reason }: { label: string; reason: string | null }) {
  return (
    <div className="metric-card metric-card-unavailable" role="group" aria-label={label}>
      <span className="metric-label">{label}</span>
      <span className="metric-value-small">{NOT_MEASURED}</span>
      <span className="metric-detail">{reason ?? 'No reason reported.'}</span>
    </div>
  );
}

function UptimeCard({ metrics }: MetricCardProps) {
  const uptime = metrics.uptimeSeconds;
  if (!uptime.available || uptime.value === null) {
    return <UnavailableCard label="Uptime" reason={uptime.reason} />;
  }
  const load = metrics.loadAverage;
  return (
    <div className="metric-card" role="group" aria-label="Uptime">
      <span className="metric-label">Uptime</span>
      <span className="metric-value">{formatUptime(uptime.value)}</span>
      {load.available && load.value !== null ? (
        <span className="metric-detail">{formatLoadAverage(load.value)}</span>
      ) : null}
    </div>
  );
}

function CpuCard({ metrics, trend }: MetricCardsProps) {
  const cpu = metrics.cpuPercent;
  if (!cpu.available || cpu.value === null) {
    return <UnavailableCard label="CPU" reason={cpu.reason} />;
  }
  const cpuCount = metrics.cpuCount;
  return (
    <div className="metric-card" role="group" aria-label="CPU">
      <span className="metric-label">
        <span>CPU</span>
        {cpuCount.available && cpuCount.value !== null ? <span>{cpuCount.value} vCPU</span> : null}
      </span>
      <span className="metric-value">{cpu.value.toFixed(0)}%</span>
      <CpuSparkline samples={trend.cpuHistory} />
    </div>
  );
}

function MemoryCard({ metrics }: MetricCardProps) {
  const memory = metrics.memory;
  if (!memory.available || memory.value === null) {
    return <UnavailableCard label="Memory" reason={memory.reason} />;
  }
  const unitIndex = unitIndexOf(memory.value.totalBytes);
  const used = formatBytes(memory.value.usedBytes, unitIndex);
  const total = formatBytes(memory.value.totalBytes, unitIndex);
  return (
    <div className="metric-card" role="group" aria-label="Memory">
      <span className="metric-label">Memory</span>
      <span className="metric-value">
        {used.amount}
        <span className="metric-unit">
          of {total.amount} {total.unit}
        </span>
      </span>
      <Meter label="Memory used" percent={percentOf(memory.value.usedBytes, memory.value.totalBytes)} />
    </div>
  );
}

function DiskCard({ metrics }: MetricCardProps) {
  const disk = metrics.disk;
  if (!disk.available || disk.value === null) {
    return <UnavailableCard label="Disk" reason={disk.reason} />;
  }
  const unitIndex = unitIndexOf(disk.value.totalBytes);
  const free = roundedAmount(formatBytes(disk.value.freeBytes, unitIndex));
  const total = roundedAmount(formatBytes(disk.value.totalBytes, unitIndex));
  const usedPercent = 100 - percentOf(disk.value.freeBytes, disk.value.totalBytes);
  return (
    <div className="metric-card" role="group" aria-label="Disk">
      <span className="metric-label">
        <span>
          Disk <code className="mono">{disk.value.mountPoint}</code>
        </span>
        <span>{usedPercent}% used</span>
      </span>
      <span className="metric-value">
        {free.amount} {free.unit}{' '}
        <span className="metric-unit">free of {total.amount}</span>
      </span>
      <Meter label="Disk used" percent={usedPercent} />
    </div>
  );
}

function roundedAmount(size: ByteSize): ByteSize {
  const amount = Number(size.amount);
  return amount >= 10 ? { ...size, amount: String(Math.round(amount)) } : size;
}

function Meter({ label, percent }: { label: string; percent: number }) {
  return <progress className="meter" aria-label={label} max={100} value={percent} />;
}

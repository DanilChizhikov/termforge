import type { ServerMetrics } from '../api/monitoring';
import { formatBytes, formatUptime, percentOf, unitIndexOf } from './metricsFormat';

export interface MetricCardsProps {
  metrics: ServerMetrics;
}

const NOT_MEASURED = 'Not measured';

export function MetricCards({ metrics }: MetricCardsProps) {
  return (
    <div className="metric-grid">
      <ConnectionCard metrics={metrics} />
      <UptimeCard metrics={metrics} />
      <CpuCard metrics={metrics} />
      <MemoryCard metrics={metrics} />
      <DiskCard metrics={metrics} />
    </div>
  );
}

function ConnectionCard({ metrics }: MetricCardsProps) {
  const connected = metrics.state === 'Connected';
  return (
    <div className="metric-card" role="group" aria-label="Connection">
      <span className="metric-label">Connection</span>
      <span className="metric-value">
        <span className={`status-dot ${connected ? 'status-dot-ok' : 'status-dot-danger'}`} aria-hidden="true" />
        {connected ? 'Connected' : 'Disconnected'}
      </span>
      {connected ? null : <span className="metric-detail">{metrics.message ?? metrics.state}</span>}
    </div>
  );
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

function UptimeCard({ metrics }: MetricCardsProps) {
  const uptime = metrics.uptimeSeconds;
  if (!uptime.available || uptime.value === null) {
    return <UnavailableCard label="Uptime" reason={uptime.reason} />;
  }
  return (
    <div className="metric-card" role="group" aria-label="Uptime">
      <span className="metric-label">Uptime</span>
      <span className="metric-value">{formatUptime(uptime.value)}</span>
    </div>
  );
}

function CpuCard({ metrics }: MetricCardsProps) {
  const cpu = metrics.cpuPercent;
  if (!cpu.available || cpu.value === null) {
    return <UnavailableCard label="CPU" reason={cpu.reason} />;
  }
  return (
    <div className="metric-card" role="group" aria-label="CPU">
      <span className="metric-label">CPU</span>
      <span className="metric-value">{cpu.value.toFixed(0)}%</span>
      <Meter label="CPU usage" percent={Math.round(cpu.value)} />
    </div>
  );
}

function MemoryCard({ metrics }: MetricCardsProps) {
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

function DiskCard({ metrics }: MetricCardsProps) {
  const disk = metrics.disk;
  if (!disk.available || disk.value === null) {
    return <UnavailableCard label="Disk" reason={disk.reason} />;
  }
  const unitIndex = unitIndexOf(disk.value.totalBytes);
  const free = formatBytes(disk.value.freeBytes, unitIndex);
  const total = formatBytes(disk.value.totalBytes, unitIndex);
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
        {free.amount} {free.unit}
        <span className="metric-unit">
          free of {total.amount} {total.unit}
        </span>
      </span>
      <Meter label="Disk used" percent={usedPercent} />
    </div>
  );
}

function Meter({ label, percent }: { label: string; percent: number }) {
  return <progress className="meter" aria-label={label} max={100} value={percent} />;
}

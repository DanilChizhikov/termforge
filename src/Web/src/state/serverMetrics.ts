import { useCallback, useSyncExternalStore } from 'react';
import { getServerMetrics, type ServerMetrics } from '../api/monitoring';
import { getMonitoringInterval, subscribeMonitoringInterval } from './monitoringInterval';

export interface CpuSample {
  at: string;
  percent: number;
}

export interface MetricsTrend {
  cpuHistory: readonly CpuSample[];
  connectedSince: string | null;
  dropsToday: number;
}

export interface MetricsSnapshot {
  metrics: ServerMetrics | null;
  error: string | null;
  isLoading: boolean;
  isPolling: boolean;
  trend: MetricsTrend;
}

interface Watch {
  snapshot: MetricsSnapshot;
  listeners: Set<() => void>;
  timer: ReturnType<typeof setTimeout> | null;
  inFlight: boolean;
  dropTimes: number[];
}

export const CPU_HISTORY_WINDOW_MINUTES = 15;

const CPU_HISTORY_WINDOW_MS = CPU_HISTORY_WINDOW_MINUTES * 60 * 1000;
const LOAD_ERROR = 'Could not load server metrics.';
const EMPTY_TREND: MetricsTrend = { cpuHistory: [], connectedSince: null, dropsToday: 0 };
const INITIAL_SNAPSHOT: MetricsSnapshot = {
  metrics: null,
  error: null,
  isLoading: true,
  isPolling: true,
  trend: EMPTY_TREND,
};

function startOfLocalDay(time: number): number {
  const day = new Date(time);
  day.setHours(0, 0, 0, 0);
  return day.getTime();
}

function countDropsToday(watch: Watch, now: number): number {
  const dayStart = startOfLocalDay(now);
  watch.dropTimes = watch.dropTimes.filter((dropTime) => dropTime >= dayStart);
  return watch.dropTimes.length;
}

function appendCpuSample(history: readonly CpuSample[], metrics: ServerMetrics): readonly CpuSample[] {
  const cpu = metrics.cpuPercent;
  if (!cpu.available || cpu.value === null) {
    return history;
  }
  const sampledAt = Date.parse(metrics.collectedAt);
  const windowStart = sampledAt - CPU_HISTORY_WINDOW_MS;
  const recent = history.filter((sample) => Date.parse(sample.at) >= windowStart);
  return [...recent, { at: metrics.collectedAt, percent: cpu.value }];
}

function trendAfterSuccess(watch: Watch, metrics: ServerMetrics): MetricsTrend {
  const previous = watch.snapshot.trend;
  if (metrics.state !== 'Connected') {
    return trendAfterFailure(watch);
  }
  return {
    cpuHistory: appendCpuSample(previous.cpuHistory, metrics),
    connectedSince: previous.connectedSince ?? metrics.collectedAt,
    dropsToday: countDropsToday(watch, Date.now()),
  };
}

function trendAfterFailure(watch: Watch): MetricsTrend {
  const previous = watch.snapshot.trend;
  const now = Date.now();
  if (previous.connectedSince !== null) {
    watch.dropTimes.push(now);
  }
  return { ...previous, connectedSince: null, dropsToday: countDropsToday(watch, now) };
}

const watches = new Map<string, Watch>();

function update(watch: Watch, snapshot: MetricsSnapshot): void {
  watch.snapshot = snapshot;
  watch.listeners.forEach((listener) => listener());
}

function clearTimer(watch: Watch): void {
  if (watch.timer !== null) {
    clearTimeout(watch.timer);
    watch.timer = null;
  }
}

function scheduleNext(serverId: string, watch: Watch): void {
  clearTimer(watch);
  watch.timer = setTimeout(() => void poll(serverId), getMonitoringInterval() * 1000);
}

async function poll(serverId: string): Promise<void> {
  const watch = watches.get(serverId);
  if (watch === undefined || watch.inFlight) {
    return;
  }
  clearTimer(watch);
  watch.inFlight = true;
  update(watch, { ...watch.snapshot, isLoading: true, isPolling: true, error: null });
  try {
    const metrics = await getServerMetrics(serverId);
    if (watches.get(serverId) !== watch) {
      return;
    }
    const connected = metrics.state === 'Connected';
    const trend = trendAfterSuccess(watch, metrics);
    update(watch, { metrics, error: null, isLoading: false, isPolling: connected, trend });
    if (connected) {
      scheduleNext(serverId, watch);
    }
  } catch {
    if (watches.get(serverId) === watch) {
      const trend = trendAfterFailure(watch);
      update(watch, { ...watch.snapshot, error: LOAD_ERROR, isLoading: false, isPolling: false, trend });
    }
  } finally {
    watch.inFlight = false;
  }
}

function subscribeToServer(serverId: string, listener: () => void): () => void {
  let watch = watches.get(serverId);
  if (watch === undefined) {
    watch = { snapshot: INITIAL_SNAPSHOT, listeners: new Set(), timer: null, inFlight: false, dropTimes: [] };
    watches.set(serverId, watch);
    void poll(serverId);
  }
  watch.listeners.add(listener);
  const current = watch;
  return () => {
    current.listeners.delete(listener);
    if (current.listeners.size === 0) {
      clearTimer(current);
      watches.delete(serverId);
    }
  };
}

subscribeMonitoringInterval(() => {
  watches.forEach((watch, serverId) => {
    if (watch.timer !== null) {
      scheduleNext(serverId, watch);
    }
  });
});

export function resumeMetrics(serverId: string): void {
  void poll(serverId);
}

export function useServerMetrics(serverId: string): MetricsSnapshot {
  const subscribe = useCallback((listener: () => void) => subscribeToServer(serverId, listener), [serverId]);
  const getSnapshot = useCallback(() => watches.get(serverId)?.snapshot ?? INITIAL_SNAPSHOT, [serverId]);
  return useSyncExternalStore(subscribe, getSnapshot, () => INITIAL_SNAPSHOT);
}

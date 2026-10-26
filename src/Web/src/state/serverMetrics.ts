import { useCallback, useSyncExternalStore } from 'react';
import { getServerMetrics, type ServerMetrics } from '../api/monitoring';
import { getMonitoringInterval, subscribeMonitoringInterval } from './monitoringInterval';

export interface MetricsSnapshot {
  metrics: ServerMetrics | null;
  error: string | null;
  isLoading: boolean;
  isPolling: boolean;
}

interface Watch {
  snapshot: MetricsSnapshot;
  listeners: Set<() => void>;
  timer: ReturnType<typeof setTimeout> | null;
  inFlight: boolean;
}

const LOAD_ERROR = 'Could not load server metrics.';
const INITIAL_SNAPSHOT: MetricsSnapshot = { metrics: null, error: null, isLoading: true, isPolling: true };

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
    update(watch, { metrics, error: null, isLoading: false, isPolling: connected });
    if (connected) {
      scheduleNext(serverId, watch);
    }
  } catch {
    if (watches.get(serverId) === watch) {
      update(watch, { ...watch.snapshot, error: LOAD_ERROR, isLoading: false, isPolling: false });
    }
  } finally {
    watch.inFlight = false;
  }
}

function subscribeToServer(serverId: string, listener: () => void): () => void {
  let watch = watches.get(serverId);
  if (watch === undefined) {
    watch = { snapshot: INITIAL_SNAPSHOT, listeners: new Set(), timer: null, inFlight: false };
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

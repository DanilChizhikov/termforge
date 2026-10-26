import { useSyncExternalStore } from 'react';

export const MONITORING_INTERVALS_SECONDS: readonly number[] = [10, 30, 60];

const DEFAULT_INTERVAL_SECONDS = 30;
const STORAGE_KEY = 'termforge.monitoring.interval';

let listeners: Array<() => void> = [];
let currentSeconds = readStoredInterval();

function readStoredInterval(): number {
  try {
    const stored = Number(window.localStorage.getItem(STORAGE_KEY));
    return MONITORING_INTERVALS_SECONDS.includes(stored) ? stored : DEFAULT_INTERVAL_SECONDS;
  } catch {
    return DEFAULT_INTERVAL_SECONDS;
  }
}

function subscribe(listener: () => void): () => void {
  listeners = [...listeners, listener];
  return () => {
    listeners = listeners.filter((item) => item !== listener);
  };
}

export function getMonitoringInterval(): number {
  return currentSeconds;
}

export function setMonitoringInterval(seconds: number): void {
  if (!MONITORING_INTERVALS_SECONDS.includes(seconds) || seconds === currentSeconds) {
    return;
  }
  currentSeconds = seconds;
  try {
    window.localStorage.setItem(STORAGE_KEY, String(seconds));
  } catch {
    currentSeconds = seconds;
  }
  listeners.forEach((listener) => listener());
}

export function subscribeMonitoringInterval(listener: () => void): () => void {
  return subscribe(listener);
}

export function useMonitoringInterval(): number {
  return useSyncExternalStore(subscribe, getMonitoringInterval, getMonitoringInterval);
}

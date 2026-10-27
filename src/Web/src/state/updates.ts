import { useEffect, useSyncExternalStore } from 'react';
import { checkForUpdates, getUpdateStatus, type UpdateStatus } from '../api/updates';

export interface UpdatesState {
  status: UpdateStatus | null;
  checking: boolean;
}

const INITIAL_STATE: UpdatesState = { status: null, checking: false };

let state: UpdatesState = INITIAL_STATE;
let loading: Promise<void> | null = null;
let listeners: Array<() => void> = [];

function subscribe(listener: () => void): () => void {
  listeners = [...listeners, listener];
  return () => {
    listeners = listeners.filter((item) => item !== listener);
  };
}

function getSnapshot(): UpdatesState {
  return state;
}

function update(next: Partial<UpdatesState>): void {
  state = { ...state, ...next };
  listeners.forEach((listener) => listener());
}

function ensureUpdateStatusLoaded(): Promise<void> {
  loading ??= getUpdateStatus()
    .then((status) => update({ status }))
    .catch(() => undefined);
  return loading;
}

export function checkUpdatesNow(): Promise<void> {
  update({ checking: true });
  return checkForUpdates()
    .then((status) => update({ status }))
    .catch(() => undefined)
    .finally(() => update({ checking: false }));
}

export function resetUpdates(): void {
  state = INITIAL_STATE;
  loading = null;
}

export function useUpdates(): UpdatesState {
  useEffect(() => {
    void ensureUpdateStatusLoaded();
  }, []);
  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}

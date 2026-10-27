import { useSyncExternalStore } from 'react';

let savedAt: Date | null = null;
let listeners: Array<() => void> = [];

function subscribe(listener: () => void): () => void {
  listeners = [...listeners, listener];
  return () => {
    listeners = listeners.filter((item) => item !== listener);
  };
}

function getSnapshot(): Date | null {
  return savedAt;
}

export function markWorkspaceSaved(at: Date): void {
  savedAt = at;
  listeners.forEach((listener) => listener());
}

export function resetWorkspaceSaved(): void {
  savedAt = null;
  listeners.forEach((listener) => listener());
}

export function useWorkspaceSavedAt(): Date | null {
  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}

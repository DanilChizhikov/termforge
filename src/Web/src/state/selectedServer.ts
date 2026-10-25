import { useSyncExternalStore } from 'react';

let selectedServerId: string | null = null;
let listeners: Array<() => void> = [];

function subscribe(listener: () => void): () => void {
  listeners = [...listeners, listener];
  return () => {
    listeners = listeners.filter((item) => item !== listener);
  };
}

function getSnapshot(): string | null {
  return selectedServerId;
}

export function selectServer(id: string | null): void {
  if (selectedServerId === id) {
    return;
  }
  selectedServerId = id;
  listeners.forEach((listener) => listener());
}

export function useSelectedServerId(): string | null {
  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}

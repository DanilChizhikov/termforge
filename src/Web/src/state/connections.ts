import { useEffect, useSyncExternalStore } from 'react';
import { listConnections, type SavedConnection } from '../api/connections';

const NO_CONNECTIONS: readonly SavedConnection[] = [];

let connections: readonly SavedConnection[] = NO_CONNECTIONS;
let loading: Promise<readonly SavedConnection[]> | null = null;
let listeners: Array<() => void> = [];

export function subscribeConnections(listener: () => void): () => void {
  listeners = [...listeners, listener];
  return () => {
    listeners = listeners.filter((item) => item !== listener);
  };
}

export function getConnections(): readonly SavedConnection[] {
  return connections;
}

export function replaceConnections(next: readonly SavedConnection[]): void {
  connections = next;
  listeners.forEach((listener) => listener());
}

export function reloadConnections(): Promise<readonly SavedConnection[]> {
  loading = listConnections().then((list) => {
    replaceConnections(list);
    return list;
  });
  return loading;
}

export function ensureConnectionsLoaded(): Promise<readonly SavedConnection[]> {
  return loading ?? reloadConnections();
}

export function findConnection(id: string | null): SavedConnection | null {
  if (id === null) {
    return null;
  }
  return connections.find((connection) => connection.id === id) ?? null;
}

export function resetConnections(): void {
  connections = NO_CONNECTIONS;
  loading = null;
  listeners.forEach((listener) => listener());
}

export function useConnections(): readonly SavedConnection[] {
  useEffect(() => {
    ensureConnectionsLoaded().catch(() => {
      loading = null;
    });
  }, []);
  return useSyncExternalStore(subscribeConnections, getConnections, getConnections);
}

export function useConnection(id: string | null): SavedConnection | null {
  const list = useConnections();
  if (id === null) {
    return null;
  }
  return list.find((connection) => connection.id === id) ?? null;
}

import { useSyncExternalStore } from 'react';

const LOCAL_KEY = 'local';

let paths: ReadonlyMap<string, string> = new Map();
let listeners: Array<() => void> = [];

function subscribe(listener: () => void): () => void {
  listeners = [...listeners, listener];
  return () => {
    listeners = listeners.filter((item) => item !== listener);
  };
}

function setPath(key: string, path: string): void {
  if (paths.get(key) === path) {
    return;
  }
  const next = new Map(paths);
  next.set(key, path);
  paths = next;
  listeners.forEach((listener) => listener());
}

function usePath(key: string): string | null {
  return useSyncExternalStore(
    subscribe,
    () => paths.get(key) ?? null,
    () => null,
  );
}

export function setLocalPath(path: string): void {
  setPath(LOCAL_KEY, path);
}

export function useLocalPath(): string | null {
  return usePath(LOCAL_KEY);
}

export function setRemotePath(serverId: string, path: string): void {
  setPath(`remote:${serverId}`, path);
}

export function useRemotePath(serverId: string): string | null {
  return usePath(`remote:${serverId}`);
}

export function resetFilePaths(): void {
  paths = new Map();
  listeners.forEach((listener) => listener());
}

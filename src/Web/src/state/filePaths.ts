import { useSyncExternalStore } from 'react';

const LOCAL_KEY = 'local';
const REMOTE_PREFIX = 'remote:';

export interface FilePathsSnapshot {
  localPath: string | null;
  remotePaths: ReadonlyMap<string, string>;
}

let paths: ReadonlyMap<string, string> = new Map();
let listeners: Array<() => void> = [];

export function subscribeFilePaths(listener: () => void): () => void {
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
    subscribeFilePaths,
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
  setPath(`${REMOTE_PREFIX}${serverId}`, path);
}

export function useRemotePath(serverId: string): string | null {
  return usePath(`${REMOTE_PREFIX}${serverId}`);
}

export function getFilePaths(): FilePathsSnapshot {
  const remotePaths = new Map<string, string>();
  for (const [key, path] of paths) {
    if (key.startsWith(REMOTE_PREFIX)) {
      remotePaths.set(key.slice(REMOTE_PREFIX.length), path);
    }
  }
  return { localPath: paths.get(LOCAL_KEY) ?? null, remotePaths };
}

export function restoreFilePaths(localPath: string | null, remotePaths: ReadonlyMap<string, string>): void {
  const next = new Map<string, string>();
  if (localPath !== null) {
    next.set(LOCAL_KEY, localPath);
  }
  for (const [serverId, path] of remotePaths) {
    next.set(`${REMOTE_PREFIX}${serverId}`, path);
  }
  paths = next;
  listeners.forEach((listener) => listener());
}

export function resetFilePaths(): void {
  paths = new Map();
  listeners.forEach((listener) => listener());
}

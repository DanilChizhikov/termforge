import { useSyncExternalStore } from 'react';

export type WorkspaceTabId = 'workspace' | 'editor' | 'docker' | 'diagnostics' | 'history';

let activeTab: WorkspaceTabId = 'workspace';
let listeners: Array<() => void> = [];

function subscribe(listener: () => void): () => void {
  listeners = [...listeners, listener];
  return () => {
    listeners = listeners.filter((item) => item !== listener);
  };
}

function getSnapshot(): WorkspaceTabId {
  return activeTab;
}

export function activateWorkspaceTab(id: WorkspaceTabId): void {
  if (activeTab === id) {
    return;
  }
  activeTab = id;
  listeners.forEach((listener) => listener());
}

export function useActiveWorkspaceTab(): WorkspaceTabId {
  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}

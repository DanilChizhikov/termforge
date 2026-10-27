import { useSyncExternalStore } from 'react';

export type WorkspaceTabId = 'workspace' | 'editor' | 'docker' | 'diagnostics' | 'history';

const WORKSPACE_TAB_IDS: readonly string[] = ['workspace', 'editor', 'docker', 'diagnostics', 'history'];

export function isWorkspaceTabId(value: string): value is WorkspaceTabId {
  return WORKSPACE_TAB_IDS.includes(value);
}

let activeTab: WorkspaceTabId = 'workspace';
let listeners: Array<() => void> = [];

export function subscribeWorkspaceTab(listener: () => void): () => void {
  listeners = [...listeners, listener];
  return () => {
    listeners = listeners.filter((item) => item !== listener);
  };
}

export function getActiveWorkspaceTab(): WorkspaceTabId {
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
  return useSyncExternalStore(subscribeWorkspaceTab, getActiveWorkspaceTab, getActiveWorkspaceTab);
}

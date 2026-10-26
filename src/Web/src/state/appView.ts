import { useSyncExternalStore } from 'react';

export type AppView = 'workspace' | 'settings';

let activeView: AppView = 'workspace';
let listeners: Array<() => void> = [];

function subscribe(listener: () => void): () => void {
  listeners = [...listeners, listener];
  return () => {
    listeners = listeners.filter((item) => item !== listener);
  };
}

function getSnapshot(): AppView {
  return activeView;
}

export function showAppView(view: AppView): void {
  if (activeView === view) {
    return;
  }
  activeView = view;
  listeners.forEach((listener) => listener());
}

export function useAppView(): AppView {
  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}

import { useSyncExternalStore } from 'react';
import { clearEditorDraft } from './editorDrafts';
import { closeEditorFile, getEditorFiles } from './editorFiles';

let pendingKey: string | null = null;
let listeners: Array<() => void> = [];

function subscribe(listener: () => void): () => void {
  listeners = [...listeners, listener];
  return () => {
    listeners = listeners.filter((item) => item !== listener);
  };
}

function getSnapshot(): string | null {
  return pendingKey;
}

function setPending(key: string | null): void {
  pendingKey = key;
  listeners.forEach((listener) => listener());
}

export function discardAndCloseEditorFile(key: string): void {
  clearEditorDraft(key);
  closeEditorFile(key);
  if (pendingKey === key) {
    setPending(null);
  }
}

export function requestEditorFileClose(key: string): void {
  const file = getEditorFiles().files.find((item) => item.key === key);
  if (file === undefined) {
    return;
  }
  if (file.dirty) {
    setPending(key);
    return;
  }
  discardAndCloseEditorFile(key);
}

export function cancelEditorFileClose(): void {
  setPending(null);
}

export function usePendingEditorClose(): string | null {
  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}

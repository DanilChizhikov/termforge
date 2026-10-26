import { useSyncExternalStore } from 'react';
import { activateWorkspaceTab } from './workspaceTab';

export interface EditorFile {
  key: string;
  connectionId: string;
  path: string;
  name: string;
  dirty: boolean;
}

export interface EditorFilesState {
  files: readonly EditorFile[];
  activeKey: string | null;
}

const EMPTY: EditorFilesState = { files: [], activeKey: null };

let state: EditorFilesState = EMPTY;
let listeners: Array<() => void> = [];

function subscribe(listener: () => void): () => void {
  listeners = [...listeners, listener];
  return () => {
    listeners = listeners.filter((item) => item !== listener);
  };
}

function getSnapshot(): EditorFilesState {
  return state;
}

function update(next: EditorFilesState): void {
  state = next;
  listeners.forEach((listener) => listener());
}

export function editorFileKey(connectionId: string, path: string): string {
  return `${connectionId}:${path}`;
}

export function fileNameOf(path: string): string {
  const index = path.lastIndexOf('/');
  return index < 0 ? path : path.slice(index + 1);
}

export function openEditorFile(connectionId: string, path: string): void {
  const key = editorFileKey(connectionId, path);
  const exists = state.files.some((file) => file.key === key);
  const files = exists ? state.files : [...state.files, { key, connectionId, path, name: fileNameOf(path), dirty: false }];
  update({ files, activeKey: key });
  activateWorkspaceTab('editor');
}

export function activateEditorFile(key: string): void {
  if (state.activeKey !== key) {
    update({ ...state, activeKey: key });
  }
}

export function closeEditorFile(key: string): void {
  const index = state.files.findIndex((file) => file.key === key);
  if (index < 0) {
    return;
  }
  const files = state.files.filter((file) => file.key !== key);
  const neighbour = files[Math.min(index, files.length - 1)] ?? null;
  const activeKey = state.activeKey === key ? (neighbour?.key ?? null) : state.activeKey;
  update({ files, activeKey });
}

export function setEditorFileDirty(key: string, dirty: boolean): void {
  const target = state.files.find((file) => file.key === key);
  if (target === undefined || target.dirty === dirty) {
    return;
  }
  update({ ...state, files: state.files.map((file) => (file.key === key ? { ...file, dirty } : file)) });
}

export function resetEditorFiles(): void {
  update(EMPTY);
}

export function useEditorFiles(): EditorFilesState {
  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}

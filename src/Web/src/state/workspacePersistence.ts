import { useSyncExternalStore } from 'react';
import { apiClient, type ApiClient } from '../api/client';
import {
  getWorkspace,
  saveWorkspace,
  type WorkspaceEditorDraft,
  type WorkspaceEditorTab,
  type WorkspaceRestore,
  type WorkspaceSnapshot,
  type WorkspaceWarning,
} from '../api/workspace';
import { getEditorDrafts, restoreEditorDrafts, subscribeEditorDrafts } from './editorDrafts';
import { editorFileKey, getEditorFiles, restoreEditorFiles, subscribeEditorFiles } from './editorFiles';
import { getFilePaths, restoreFilePaths, subscribeFilePaths } from './filePaths';
import { getSelectedServerId, selectServer, subscribeSelectedServer } from './selectedServer';
import { getWorkspaceLayout, restoreWorkspaceLayout, subscribeWorkspaceLayout } from './workspaceLayout';
import {
  activateWorkspaceTab,
  getActiveWorkspaceTab,
  isWorkspaceTabId,
  subscribeWorkspaceTab,
} from './workspaceTab';
import {
  getRestoredTerminals,
  getTerminalTabsSnapshot,
  restoreTerminalTabs,
  subscribeTerminalTabs,
} from './workspaceTerminals';

export const WORKSPACE_SAVE_DELAY_MS = 1000;
const MAX_REMOTE_PATHS = 200;

export interface WorkspaceAutosaveOptions {
  delayMs?: number;
  client?: ApiClient;
}

interface AutosaveController {
  flush: () => Promise<void>;
}

let warning: WorkspaceWarning | null = null;
let warningListeners: Array<() => void> = [];
let autosave: AutosaveController | null = null;

function setWarning(next: WorkspaceWarning | null): void {
  warning = next;
  warningListeners.forEach((listener) => listener());
}

function subscribeWarning(listener: () => void): () => void {
  warningListeners = [...warningListeners, listener];
  return () => {
    warningListeners = warningListeners.filter((item) => item !== listener);
  };
}

function getWarning(): WorkspaceWarning | null {
  return warning;
}

export function useWorkspaceRestoreWarning(): WorkspaceWarning | null {
  return useSyncExternalStore(subscribeWarning, getWarning, getWarning);
}

export function dismissWorkspaceRestoreWarning(): void {
  setWarning(null);
}

export function applyWorkspaceRestore(restore: WorkspaceRestore): void {
  const { state, drafts } = restore;
  selectServer(state.selectedConnectionId);
  if (state.activeView !== null && isWorkspaceTabId(state.activeView)) {
    activateWorkspaceTab(state.activeView);
  }
  const remotePaths = new Map(state.remotePaths.map((remote) => [remote.connectionId, remote.path]));
  restoreFilePaths(state.localPath, remotePaths);
  restoreWorkspaceLayout(state.panelLayouts, state.collapsedPanels);
  restoreTerminalTabs(state.terminalTabs, state.activeTerminalKey);
  restoreEditorDrafts(drafts);
  const draftKeys = new Set(drafts.map((draft) => editorFileKey(draft.connectionId, draft.path)));
  const editorFiles = state.editorTabs.map((tab) => ({
    connectionId: tab.connectionId,
    path: tab.path,
    dirty: draftKeys.has(editorFileKey(tab.connectionId, tab.path)),
  }));
  const activeEditor = state.activeEditorTab;
  const activeEditorKey = activeEditor === null ? null : editorFileKey(activeEditor.connectionId, activeEditor.path);
  restoreEditorFiles(editorFiles, activeEditorKey);
  setWarning(restore.warning);
}

export async function restoreWorkspace(client: ApiClient = apiClient): Promise<boolean> {
  try {
    const restore = await getWorkspace(client);
    applyWorkspaceRestore(restore);
    return true;
  } catch {
    return false;
  }
}

function currentTerminals() {
  const restored = getRestoredTerminals();
  if (restored.tabs.length > 0) {
    return {
      tabs: restored.tabs.map((tab) => ({
        key: tab.key,
        connectionId: tab.connectionId,
        connectionName: tab.connectionName ?? '',
      })),
      activeKey: restored.activeKey,
    };
  }
  return getTerminalTabsSnapshot();
}

export function buildWorkspaceSnapshot(): WorkspaceSnapshot {
  const terminals = currentTerminals();
  const filePaths = getFilePaths();
  const layout = getWorkspaceLayout();
  const editor = getEditorFiles();
  const drafts = getEditorDrafts();
  const editorTabs: WorkspaceEditorTab[] = editor.files.map((file) => ({ connectionId: file.connectionId, path: file.path }));
  const activeFile = editor.files.find((file) => file.key === editor.activeKey) ?? null;
  const editorDrafts: WorkspaceEditorDraft[] = [];
  for (const file of editor.files) {
    const draft = drafts.get(file.key);
    if (draft !== undefined) {
      editorDrafts.push({ connectionId: file.connectionId, path: file.path, content: draft.content, baseVersion: draft.baseVersion });
    }
  }
  return {
    state: {
      selectedConnectionId: getSelectedServerId(),
      activeView: getActiveWorkspaceTab(),
      terminalTabs: [...terminals.tabs],
      activeTerminalKey: terminals.activeKey,
      localPath: filePaths.localPath,
      remotePaths: [...filePaths.remotePaths]
        .map(([connectionId, path]) => ({ connectionId, path }))
        .slice(-MAX_REMOTE_PATHS),
      panelLayouts: [...layout.layouts].map(([groupId, sizes]) => ({ groupId, sizes: { ...sizes } })),
      collapsedPanels: [...layout.collapsed],
      editorTabs,
      activeEditorTab: activeFile === null ? null : { connectionId: activeFile.connectionId, path: activeFile.path },
    },
    drafts: editorDrafts,
  };
}

export function startWorkspaceAutosave(options: WorkspaceAutosaveOptions = {}): () => void {
  const delayMs = options.delayMs ?? WORKSPACE_SAVE_DELAY_MS;
  const client = options.client ?? apiClient;
  let timer: ReturnType<typeof setTimeout> | null = null;
  let lastSaved = JSON.stringify(buildWorkspaceSnapshot());
  let pending: Promise<void> = Promise.resolve();

  const flush = (): Promise<void> => {
    if (timer !== null) {
      clearTimeout(timer);
      timer = null;
    }
    const snapshot = buildWorkspaceSnapshot();
    const serialized = JSON.stringify(snapshot);
    if (serialized === lastSaved) {
      return pending;
    }
    pending = pending
      .then(() => saveWorkspace(snapshot, client))
      .then(() => {
        lastSaved = serialized;
      })
      .catch(() => undefined);
    return pending;
  };

  const schedule = () => {
    if (timer !== null) {
      clearTimeout(timer);
    }
    timer = setTimeout(() => void flush(), delayMs);
  };

  const flushOnHide = () => void flush();
  const unsubscribers = [
    subscribeSelectedServer(schedule),
    subscribeWorkspaceTab(schedule),
    subscribeFilePaths(schedule),
    subscribeWorkspaceLayout(schedule),
    subscribeTerminalTabs(schedule),
    subscribeEditorFiles(schedule),
    subscribeEditorDrafts(schedule),
  ];
  window.addEventListener('pagehide', flushOnHide);
  const controller: AutosaveController = { flush };
  autosave = controller;

  return () => {
    if (timer !== null) {
      clearTimeout(timer);
      timer = null;
    }
    unsubscribers.forEach((unsubscribe) => unsubscribe());
    window.removeEventListener('pagehide', flushOnHide);
    if (autosave === controller) {
      autosave = null;
    }
  };
}

export function flushWorkspaceState(): Promise<void> {
  return autosave?.flush() ?? Promise.resolve();
}

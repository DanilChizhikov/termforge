import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiClient } from '../api/client';
import type { WorkspaceRestore, WorkspaceSnapshot } from '../api/workspace';
import { getEditorDraft, resetEditorDrafts, setEditorDraft } from './editorDrafts';
import { editorFileKey, getEditorFiles, resetEditorFiles } from './editorFiles';
import { getFilePaths, resetFilePaths, setLocalPath } from './filePaths';
import { getSelectedServerId, selectServer } from './selectedServer';
import { getPanelLayout, isPanelCollapsed, recordPanelLayout, resetWorkspaceLayout } from './workspaceLayout';
import {
  applyWorkspaceRestore,
  buildWorkspaceSnapshot,
  dismissWorkspaceRestoreWarning,
  flushWorkspaceState,
  restoreWorkspace,
  startWorkspaceAutosave,
  useWorkspaceRestoreWarning,
} from './workspacePersistence';
import { activateWorkspaceTab, getActiveWorkspaceTab } from './workspaceTab';
import { getRestoredTerminals, resetWorkspaceTerminals } from './workspaceTerminals';

const WEB = '0f8fad5b-d9cb-469f-a165-70867728950e';
const DB = '7c9e6679-7425-40de-944b-e07fc1f90ae7';
const BASE_VERSION = { modifiedUtc: '2026-10-26T10:00:00Z', size: 10, sha256: 'a'.repeat(64) };

const RESTORE: WorkspaceRestore = {
  state: {
    selectedConnectionId: WEB,
    activeView: 'editor',
    terminalTabs: [
      { key: 'terminal-1', connectionId: WEB, connectionName: 'web' },
      { key: 'terminal-2', connectionId: DB, connectionName: 'db' },
    ],
    activeTerminalKey: 'terminal-2',
    localPath: '/home/dev',
    remotePaths: [{ connectionId: WEB, path: '/var/www' }],
    panelLayouts: [{ groupId: 'app-layout', sizes: { servers: 30, main: 70 } }],
    collapsedPanels: ['remote-files'],
    editorTabs: [
      { connectionId: WEB, path: '/etc/hosts' },
      { connectionId: DB, path: '/etc/motd' },
    ],
    activeEditorTab: { connectionId: DB, path: '/etc/motd' },
  },
  drafts: [{ connectionId: WEB, path: '/etc/hosts', content: '127.0.0.1 web', baseVersion: BASE_VERSION }],
  warning: null,
};

interface Recorded {
  method: string;
  url: string;
  body: unknown;
}

let requests: Recorded[];
let client: ApiClient;
let stopAutosave: (() => void) | null;

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status });
}

function createClient(handler: (request: Recorded) => Response): ApiClient {
  const fetchImpl = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
    const request: Recorded = {
      method: init?.method ?? 'GET',
      url: String(input),
      body: typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined,
    };
    requests.push(request);
    if (request.url === '/api/session') {
      return Promise.resolve(json({ csrfToken: 'token' }));
    }
    return Promise.resolve(handler(request));
  });
  return new ApiClient({ fetchImpl });
}

function saves(): Recorded[] {
  return requests.filter((request) => request.method === 'PUT' && request.url === '/api/workspace');
}

beforeEach(() => {
  requests = [];
  stopAutosave = null;
  client = createClient(() => new Response(null, { status: 204 }));
  selectServer(null);
  activateWorkspaceTab('workspace');
  resetFilePaths();
  resetWorkspaceLayout();
  resetWorkspaceTerminals();
  resetEditorFiles();
  resetEditorDrafts();
  dismissWorkspaceRestoreWarning();
});

afterEach(() => {
  stopAutosave?.();
  vi.useRealTimers();
});

describe('restoring the workspace', () => {
  it('applies the saved selection, paths, layout, terminals, editor tabs and drafts', () => {
    applyWorkspaceRestore(RESTORE);

    expect(getSelectedServerId()).toBe(WEB);
    expect(getActiveWorkspaceTab()).toBe('editor');
    expect(getFilePaths().localPath).toBe('/home/dev');
    expect(getFilePaths().remotePaths.get(WEB)).toBe('/var/www');
    expect(getPanelLayout('app-layout')).toEqual({ servers: 30, main: 70 });
    expect(isPanelCollapsed('remote-files')).toBe(true);
    const terminals = getRestoredTerminals();
    expect(terminals.tabs.map((tab) => [tab.connectionId, tab.connectionName, tab.state, tab.restored])).toEqual([
      [WEB, 'web', 'disconnected', true],
      [DB, 'db', 'disconnected', true],
    ]);
    expect(terminals.activeKey).toBe(terminals.tabs[1]!.key);
    const editor = getEditorFiles();
    expect(editor.files.map((file) => [file.path, file.restored, file.dirty])).toEqual([
      ['/etc/hosts', true, true],
      ['/etc/motd', true, false],
    ]);
    expect(editor.activeKey).toBe(editorFileKey(DB, '/etc/motd'));
    expect(getEditorDraft(editorFileKey(WEB, '/etc/hosts'))?.content).toBe('127.0.0.1 web');
  });

  it('builds the same snapshot again from the restored stores', () => {
    applyWorkspaceRestore(RESTORE);

    const snapshot = buildWorkspaceSnapshot();

    const { terminalTabs, activeTerminalKey, ...rest } = snapshot.state;
    const { terminalTabs: savedTabs, activeTerminalKey: savedActive, ...savedRest } = RESTORE.state;
    expect(rest).toEqual(savedRest);
    expect(terminalTabs.map(({ connectionId, connectionName }) => ({ connectionId, connectionName }))).toEqual(
      savedTabs.map(({ connectionId, connectionName }) => ({ connectionId, connectionName })),
    );
    expect(activeTerminalKey).toBe(terminalTabs[1]!.key);
    expect(savedActive).toBe('terminal-2');
    expect(snapshot.drafts).toEqual(RESTORE.drafts);
  });

  it('ignores an unknown saved view', () => {
    applyWorkspaceRestore({ ...RESTORE, state: { ...RESTORE.state, activeView: 'removed-view' } });

    expect(getActiveWorkspaceTab()).toBe('workspace');
  });

  it('surfaces the reset warning until it is dismissed', () => {
    const warning = renderHook(() => useWorkspaceRestoreWarning());

    act(() =>
      applyWorkspaceRestore({
        ...RESTORE,
        warning: { code: 'Corrupted', message: 'The saved workspace could not be read and was reset.' },
      }),
    );
    expect(warning.result.current?.code).toBe('Corrupted');

    act(() => dismissWorkspaceRestoreWarning());
    expect(warning.result.current).toBeNull();
  });

  it('reports failure when the saved workspace cannot be fetched', async () => {
    const failing = createClient(() => json({ title: 'boom' }, 500));

    await expect(restoreWorkspace(failing)).resolves.toBe(false);
  });

  it('fetches and applies the saved workspace', async () => {
    const restoring = createClient(() => json(RESTORE));

    await expect(restoreWorkspace(restoring)).resolves.toBe(true);
    expect(getSelectedServerId()).toBe(WEB);
  });
});

describe('saving the workspace', () => {
  it('debounces changes into one save of the latest snapshot', async () => {
    vi.useFakeTimers();
    stopAutosave = startWorkspaceAutosave({ delayMs: 1000, client });

    selectServer(WEB);
    setLocalPath('/home/dev');
    recordPanelLayout('file-panes', { 'local-files': 40.123, 'remote-files': 59.877 });
    await vi.advanceTimersByTimeAsync(999);
    expect(saves()).toHaveLength(0);
    await vi.advanceTimersByTimeAsync(1);
    await vi.runAllTimersAsync();

    expect(saves()).toHaveLength(1);
    const body = saves()[0]!.body as WorkspaceSnapshot;
    expect(body.state.selectedConnectionId).toBe(WEB);
    expect(body.state.localPath).toBe('/home/dev');
    expect(body.state.panelLayouts).toEqual([{ groupId: 'file-panes', sizes: { 'local-files': 40.12, 'remote-files': 59.88 } }]);
  });

  it('does not save when nothing changed since the last save', async () => {
    vi.useFakeTimers();
    applyWorkspaceRestore(RESTORE);
    stopAutosave = startWorkspaceAutosave({ delayMs: 1000, client });

    selectServer(DB);
    selectServer(WEB);
    await vi.runAllTimersAsync();

    expect(saves()).toHaveLength(0);
  });

  it('flushes a pending change immediately', async () => {
    stopAutosave = startWorkspaceAutosave({ delayMs: 60_000, client });
    applyWorkspaceRestore(RESTORE);
    setEditorDraft(editorFileKey(WEB, '/etc/hosts'), {
      connectionId: WEB,
      path: '/etc/hosts',
      content: '127.0.0.1 web edited',
      baseVersion: BASE_VERSION,
    });

    await flushWorkspaceState();

    expect(saves()).toHaveLength(1);
    const body = saves()[0]!.body as WorkspaceSnapshot;
    expect(body.drafts).toEqual([
      { connectionId: WEB, path: '/etc/hosts', content: '127.0.0.1 web edited', baseVersion: BASE_VERSION },
    ]);
  });

  it('flush does nothing without autosave', async () => {
    selectServer(WEB);

    await flushWorkspaceState();

    expect(saves()).toHaveLength(0);
  });
});

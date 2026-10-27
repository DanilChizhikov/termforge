import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { SavedConnection } from '../api/connections';
import { replaceConnections } from '../state/connections';
import { openEditorFile, resetEditorFiles, setEditorFileDirty, editorFileKey } from '../state/editorFiles';
import { selectServer } from '../state/selectedServer';
import { activateWorkspaceTab } from '../state/workspaceTab';
import { WorkspaceTabs } from './WorkspaceTabs';

vi.mock('./MainWorkspace', () => ({ MainWorkspace: () => <p>main workspace</p> }));
vi.mock('./editor/EditorDocumentView', () => ({ EditorDocumentView: () => <p>editor document</p> }));

const CONNECTION: SavedConnection = {
  id: 'c1',
  name: 'web-01',
  groupName: null,
  host: 'web.example',
  port: 22,
  userName: 'deploy',
  authMethod: 'Agent',
  privateKeyPath: null,
  jumpHostId: null,
  sshAlias: null,
  hasPassword: false,
  lastConnectedAt: null,
  reconnectMode: 'Manual',
  reconnectMaxAttempts: 5,
  reconnectInitialDelaySeconds: 2,
  reconnectMaxDelaySeconds: 60,
  readOnly: false,
};

function respond(body: unknown, status = 200): Promise<Response> {
  return Promise.resolve(new Response(JSON.stringify(body), { status }));
}

beforeEach(() => {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/operations')) {
        return respond({ items: [], total: 2, offset: 0, limit: 20 });
      }
      return respond(null, 401);
    }),
  );
  replaceConnections([CONNECTION]);
  selectServer('c1');
});

afterEach(() => {
  vi.unstubAllGlobals();
  selectServer(null);
  resetEditorFiles();
  activateWorkspaceTab('workspace');
});

describe('WorkspaceTabs', () => {
  it('names server tools after the selected server and counts failed operations', async () => {
    render(<WorkspaceTabs />);

    expect(screen.getByRole('tab', { name: 'Docker · web-01' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Diagnostics · web-01' })).toBeInTheDocument();
    expect(await screen.findByRole('tab', { name: 'Journal 2 failed' })).toBeInTheDocument();
  });

  it('shows a tab for each open file with its server and unsaved state', async () => {
    const user = userEvent.setup();
    render(<WorkspaceTabs />);

    act(() => openEditorFile('c1', '/etc/nginx/site.conf'));
    act(() => setEditorFileDirty(editorFileKey('c1', '/etc/nginx/site.conf'), true));

    const fileTab = screen.getByRole('tab', { name: 'site.conf · web-01 (unsaved)' });
    expect(fileTab).toHaveAttribute('aria-selected', 'true');
    await user.click(screen.getByRole('tab', { name: 'Terminals & files' }));
    expect(fileTab).toHaveAttribute('aria-selected', 'false');
    await user.click(fileTab);
    expect(fileTab).toHaveAttribute('aria-selected', 'true');
  });

  it('asks before closing a file with unsaved changes', async () => {
    const user = userEvent.setup();
    render(<WorkspaceTabs />);
    act(() => openEditorFile('c1', '/etc/nginx/site.conf'));
    act(() => setEditorFileDirty(editorFileKey('c1', '/etc/nginx/site.conf'), true));

    await user.click(screen.getByRole('button', { name: 'Close site.conf' }));

    expect(screen.getByRole('alertdialog', { name: 'Discard unsaved changes' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Discard and close' }));
    expect(screen.queryByRole('tab', { name: /site.conf/ })).not.toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Terminals & files' })).toHaveAttribute('aria-selected', 'true');
  });
});

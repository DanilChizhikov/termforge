import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';
import type { WorkspaceRestore } from './api/workspace';
import { resetEditorDrafts } from './state/editorDrafts';
import { resetEditorFiles } from './state/editorFiles';
import { resetFilePaths } from './state/filePaths';
import { selectServer } from './state/selectedServer';
import { isPanelCollapsed, resetWorkspaceLayout } from './state/workspaceLayout';
import { applyWorkspaceRestore, dismissWorkspaceRestoreWarning } from './state/workspacePersistence';
import { activateWorkspaceTab } from './state/workspaceTab';
import { resetWorkspaceTerminals } from './state/workspaceTerminals';

function restoreWith(overrides: Partial<WorkspaceRestore['state']>, warning: WorkspaceRestore['warning'] = null): void {
  applyWorkspaceRestore({
    state: {
      selectedConnectionId: null,
      activeView: null,
      terminalTabs: [],
      activeTerminalKey: null,
      localPath: null,
      remotePaths: [],
      panelLayouts: [],
      collapsedPanels: [],
      editorTabs: [],
      activeEditorTab: null,
      ...overrides,
    },
    drafts: [],
    warning,
  });
}

beforeEach(() => {
  const unauthorized = () => Promise.resolve(new Response(null, { status: 401 }));
  vi.stubGlobal('fetch', vi.fn(unauthorized));
  selectServer(null);
  activateWorkspaceTab('workspace');
  resetFilePaths();
  resetWorkspaceLayout();
  resetWorkspaceTerminals();
  resetEditorFiles();
  resetEditorDrafts();
  dismissWorkspaceRestoreWarning();
});

describe('restored layout', () => {
  it('starts with the saved collapsed regions collapsed and the others expanded', () => {
    restoreWith({ collapsedPanels: ['servers', 'remote-files'] });

    render(<App />);

    expect(screen.getByRole('button', { name: 'Expand Servers' })).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByText('No servers yet.')).not.toBeInTheDocument();
    const remote = screen.getByRole('region', { name: 'Remote' });
    expect(within(remote).getByRole('button', { name: 'Expand Remote' })).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByRole('button', { name: 'Collapse Terminals' })).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('button', { name: 'Collapse Local' })).toHaveAttribute('aria-expanded', 'true');
  });

  it('remembers a region expanded after restore', async () => {
    const user = userEvent.setup();
    restoreWith({ collapsedPanels: ['servers'] });
    render(<App />);

    await user.click(screen.getByRole('button', { name: 'Expand Servers' }));

    expect(isPanelCollapsed('servers')).toBe(false);
  });

  it('remembers a region collapsed by the user', async () => {
    const user = userEvent.setup();
    render(<App />);

    await user.click(screen.getByRole('button', { name: 'Collapse Files' }));

    expect(isPanelCollapsed('files')).toBe(true);
  });

  it('opens the saved workspace view', () => {
    restoreWith({ activeView: 'docker' });

    render(<App />);

    expect(screen.getByRole('tab', { name: 'Docker' })).toHaveAttribute('aria-selected', 'true');
  });

  it('warns that a corrupted workspace was reset', async () => {
    const user = userEvent.setup();
    restoreWith({}, { code: 'Corrupted', message: 'The saved workspace could not be read and was reset.' });
    render(<App />);

    const notice = screen.getByText('The saved workspace could not be read and was reset.');
    expect(notice.closest('[role="alert"]')).not.toBeNull();
    await user.click(screen.getByRole('button', { name: 'Dismiss' }));

    expect(screen.queryByText('The saved workspace could not be read and was reset.')).not.toBeInTheDocument();
  });
});

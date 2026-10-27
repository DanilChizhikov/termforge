import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { CommandHistoryEntry } from '../api/commandHistory';
import { CommandHistoryPanel } from './CommandHistoryPanel';

const mocks = vi.hoisted(() => ({
  listCommandHistory: vi.fn(),
  pinCommand: vi.fn(() => Promise.resolve()),
  deleteCommand: vi.fn(() => Promise.resolve()),
  clearCommandHistory: vi.fn(() => Promise.resolve()),
  getShellIntegration: vi.fn(),
}));

vi.mock('../api/commandHistory', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/commandHistory')>()),
  ...mocks,
}));

function entry(overrides: Partial<CommandHistoryEntry>): CommandHistoryEntry {
  return {
    id: 'e1',
    connectionId: 'c1',
    command: 'ls -la',
    executedAt: '2026-10-26T10:00:00Z',
    exitCode: 0,
    isPinned: false,
    ...overrides,
  };
}

function renderPanel(onInsert = vi.fn()) {
  render(
    <CommandHistoryPanel
      connectionId="c1"
      connectionName="web-01"
      version={0}
      onInsert={onInsert}
      onCollapse={vi.fn()}
    />,
  );
  return onInsert;
}

beforeEach(() => {
  Object.values(mocks).forEach((mock) => mock.mockClear());
  mocks.listCommandHistory.mockResolvedValue([]);
});

describe('CommandHistoryPanel', () => {
  it('separates pinned from recent commands and flags failed exit codes', async () => {
    mocks.listCommandHistory.mockResolvedValue([
      entry({ id: 'p', command: 'docker ps', isPinned: true }),
      entry({ id: 'r1', command: 'make test', exitCode: 2 }),
      entry({ id: 'r2', command: 'uptime' }),
    ]);
    renderPanel();

    const pinned = await screen.findByRole('region', { name: 'Pinned commands' });
    const recent = screen.getByRole('region', { name: 'Recent commands' });

    expect(pinned).toHaveTextContent('docker ps');
    expect(recent).toHaveTextContent('make test');
    expect(recent).toHaveTextContent('exit 2');
    expect(recent).not.toHaveTextContent('docker ps');
    expect(screen.getByText('Commands only — terminal output is never stored.')).toBeInTheDocument();
  });

  it('inserts a command into the terminal without running it', async () => {
    mocks.listCommandHistory.mockResolvedValue([entry({ command: 'systemctl restart nginx' })]);
    const onInsert = renderPanel();

    await userEvent.click(await screen.findByRole('button', { name: 'Insert systemctl restart nginx into the terminal' }));

    expect(onInsert).toHaveBeenCalledWith('systemctl restart nginx');
    expect(onInsert.mock.calls[0]![0]).not.toMatch(/[\r\n]/);
    expect(screen.getByText(/Press Enter in the terminal/)).toBeInTheDocument();
  });

  it('refuses to insert multi-line commands', async () => {
    mocks.listCommandHistory.mockResolvedValue([entry({ command: 'echo a\necho b' })]);
    renderPanel();

    const button = await screen.findByRole('button', { name: /Insert echo a/ });

    expect(button).toBeDisabled();
  });

  it('pins and unpins through the api and reloads', async () => {
    mocks.listCommandHistory.mockResolvedValue([entry({ id: 'x', command: 'uptime' })]);
    renderPanel();

    await userEvent.click(await screen.findByRole('button', { name: 'Pin uptime' }));

    expect(mocks.pinCommand).toHaveBeenCalledWith('x', true);
    await waitFor(() => expect(mocks.listCommandHistory).toHaveBeenCalledTimes(2));
  });

  it('searches the server history', async () => {
    renderPanel();
    await screen.findByLabelText('Search history');

    await userEvent.type(screen.getByLabelText('Search history'), 'docker');

    await waitFor(() => expect(mocks.listCommandHistory).toHaveBeenLastCalledWith('c1', 'docker'));
  });

  it('deletes an entry and clears unpinned entries', async () => {
    mocks.listCommandHistory.mockResolvedValue([entry({ id: 'd1', command: 'history' })]);
    renderPanel();

    await userEvent.click(await screen.findByRole('button', { name: 'Delete history from history' }));
    await userEvent.click(screen.getByRole('button', { name: 'Clear unpinned' }));

    expect(mocks.deleteCommand).toHaveBeenCalledWith('d1');
    expect(mocks.clearCommandHistory).toHaveBeenCalledWith('c1');
  });

  it('inserts the shell integration snippet without a newline when asked', async () => {
    mocks.getShellIntegration.mockResolvedValue({ shell: 'bash', snippet: ' __tf_e(){ :; }' });
    const onInsert = renderPanel();
    await screen.findByLabelText('Search history');

    await userEvent.click(screen.getByRole('button', { name: 'bash' }));

    await waitFor(() => expect(onInsert).toHaveBeenCalledWith(' __tf_e(){ :; }'));
    expect(mocks.getShellIntegration).toHaveBeenCalledWith('bash');
  });

  it('explains why history is empty and shows load failures', async () => {
    mocks.listCommandHistory.mockRejectedValue(new Error('boom'));
    renderPanel();

    expect(await screen.findByText('Request failed.')).toBeInTheDocument();
    expect(screen.getByText(/No commands recorded/)).toBeInTheDocument();
  });
});

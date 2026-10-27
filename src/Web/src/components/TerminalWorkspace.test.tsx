import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useEffect, useState } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { TerminalSession } from '../api/terminals';
import { selectServer } from '../state/selectedServer';
import type { TerminalViewProps } from './TerminalView';
import { TerminalWorkspace } from './TerminalWorkspace';

const mocks = vi.hoisted(() => ({
  views: [] as TerminalViewProps[],
  closeTerminal: vi.fn(() => Promise.resolve()),
}));

vi.mock('./TerminalView', () => ({
  TerminalView: function MockTerminalView(props: TerminalViewProps) {
    const [index] = useState(() => mocks.views.push(props) - 1);
    useEffect(() => {
      mocks.views[index] = props;
    });
    return <div data-testid="terminal-view" data-connection={props.connectionId} data-active={String(props.active)} />;
  },
}));

vi.mock('../api/terminals', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/terminals')>()),
  closeTerminal: mocks.closeTerminal,
}));

function session(connectionId: string, connectionName: string, sessionNumber: number): TerminalSession {
  return {
    id: `${connectionName}-session-${sessionNumber}`,
    connectionId,
    connectionName,
    sessionNumber,
    openedAt: '2026-10-26T10:00:00Z',
  };
}

function latestPropsFor(connectionId: string, index = 0): TerminalViewProps {
  const matching = mocks.views.filter((props) => props.connectionId === connectionId);
  return matching[index]!;
}

beforeEach(() => {
  mocks.views.length = 0;
  mocks.closeTerminal.mockClear();
  selectServer(null);
});

describe('TerminalWorkspace', () => {
  it('requires a selected server before opening a terminal', () => {
    render(<TerminalWorkspace />);

    expect(screen.getByRole('button', { name: /^New session/ })).toBeDisabled();
    expect(screen.getByText('Select a server to open a terminal.')).toBeInTheDocument();
    expect(screen.getByText(/No terminal sessions/)).toBeInTheDocument();
  });

  it('opens a tab for the selected server and shows its session once connected', async () => {
    const user = userEvent.setup();
    act(() => selectServer('web-id'));
    render(<TerminalWorkspace />);

    await user.click(screen.getByRole('button', { name: /^New session/ }));
    expect(screen.getByRole('tab', { name: 'Connecting… (connecting)' })).toBeInTheDocument();
    expect(screen.getByTestId('terminal-view')).toHaveAttribute('data-connection', 'web-id');

    const view = latestPropsFor('web-id');
    act(() => view.onOpened(session('web-id', 'web', 1)));
    act(() => view.onStateChange('connected', null));

    expect(screen.getByRole('tab', { name: 'web #1 (connected)' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('group', { name: 'web' })).toBeInTheDocument();
  });

  it('groups tabs by server', async () => {
    const user = userEvent.setup();
    act(() => selectServer('web-id'));
    render(<TerminalWorkspace />);
    await user.click(screen.getByRole('button', { name: /^New session/ }));
    await user.click(screen.getByRole('button', { name: /^New session/ }));
    act(() => selectServer('db-id'));
    await user.click(screen.getByRole('button', { name: /^New session/ }));

    act(() => latestPropsFor('web-id', 0).onOpened(session('web-id', 'web', 1)));
    act(() => latestPropsFor('web-id', 1).onOpened(session('web-id', 'web', 2)));
    act(() => latestPropsFor('db-id', 0).onOpened(session('db-id', 'db', 1)));

    const webGroup = screen.getByRole('group', { name: 'web' });
    const dbGroup = screen.getByRole('group', { name: 'db' });
    expect(within(webGroup).getAllByRole('tab').map((tab) => tab.textContent)).toEqual([
      'web #1 (connecting)',
      'web #2 (connecting)',
    ]);
    expect(within(dbGroup).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['db #1 (connecting)']);
    expect(within(dbGroup).getByRole('tab')).toHaveAttribute('aria-selected', 'true');
  });

  it('switches the visible terminal without unmounting the others', async () => {
    const user = userEvent.setup();
    act(() => selectServer('web-id'));
    render(<TerminalWorkspace />);
    await user.click(screen.getByRole('button', { name: /^New session/ }));
    await user.click(screen.getByRole('button', { name: /^New session/ }));
    act(() => latestPropsFor('web-id', 0).onOpened(session('web-id', 'web', 1)));
    act(() => latestPropsFor('web-id', 1).onOpened(session('web-id', 'web', 2)));

    await user.click(screen.getByRole('tab', { name: 'web #1 (connecting)' }));

    const views = screen.getAllByTestId('terminal-view');
    expect(views).toHaveLength(2);
    expect(views[0]).toHaveAttribute('data-active', 'true');
    expect(views[1]).toHaveAttribute('data-active', 'false');
    expect(screen.getByRole('tabpanel', { name: 'web #1 (connecting)' })).toBeVisible();
  });

  it('shows the disconnect reason', async () => {
    const user = userEvent.setup();
    act(() => selectServer('web-id'));
    render(<TerminalWorkspace />);
    await user.click(screen.getByRole('button', { name: /^New session/ }));
    const view = latestPropsFor('web-id');
    act(() => view.onOpened(session('web-id', 'web', 1)));

    act(() => view.onStateChange('disconnected', 'The remote shell exited or the SSH connection was lost.'));

    expect(screen.getByRole('tab', { name: 'web #1 (disconnected)' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Disconnected: The remote shell exited');
  });

  it('closes the session on the server when the tab is closed', async () => {
    const user = userEvent.setup();
    act(() => selectServer('web-id'));
    render(<TerminalWorkspace />);
    await user.click(screen.getByRole('button', { name: /^New session/ }));
    act(() => latestPropsFor('web-id').onOpened(session('web-id', 'web', 1)));

    await user.click(screen.getByRole('button', { name: 'Close web #1' }));

    expect(mocks.closeTerminal).toHaveBeenCalledWith('web-session-1');
    expect(screen.queryByRole('tab')).not.toBeInTheDocument();
    expect(screen.getByText(/No terminal sessions/)).toBeInTheDocument();
  });
});

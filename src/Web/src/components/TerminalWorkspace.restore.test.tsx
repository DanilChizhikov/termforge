import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useEffect, useState } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { selectServer } from '../state/selectedServer';
import { getTerminalTabsSnapshot, resetWorkspaceTerminals, restoreTerminalTabs } from '../state/workspaceTerminals';
import type { TerminalViewProps } from './TerminalView';
import { TerminalWorkspace } from './TerminalWorkspace';

const mocks = vi.hoisted(() => ({
  views: [] as TerminalViewProps[],
}));

vi.mock('./TerminalView', () => ({
  TerminalView: function MockTerminalView(props: TerminalViewProps) {
    const [index] = useState(() => mocks.views.push(props) - 1);
    useEffect(() => {
      mocks.views[index] = props;
    });
    return <div data-testid="terminal-view" data-connection={props.connectionId} />;
  },
}));

vi.mock('./ReconnectBar', () => ({
  ReconnectBar: () => <p>reconnect bar</p>,
}));

vi.mock('./CommandHistoryPanel', () => ({
  CommandHistoryPanel: () => <aside aria-label="Command history" />,
}));

beforeEach(() => {
  mocks.views.length = 0;
  resetWorkspaceTerminals();
  selectServer(null);
});

describe('TerminalWorkspace restore', () => {
  it('shows restored tabs disconnected without opening a shell', () => {
    restoreTerminalTabs(
      [
        { key: 'terminal-4', connectionId: 'web-id', connectionName: 'web' },
        { key: 'terminal-9', connectionId: 'db-id', connectionName: 'db' },
      ],
      'terminal-9',
    );

    render(<TerminalWorkspace />);

    const webGroup = screen.getByRole('group', { name: 'web' });
    expect(within(webGroup).getByRole('tab')).toHaveTextContent('web (restored)');
    const dbTab = within(screen.getByRole('group', { name: 'db' })).getByRole('tab');
    expect(dbTab).toHaveAttribute('aria-selected', 'true');
    expect(screen.queryByTestId('terminal-view')).not.toBeInTheDocument();
    expect(screen.queryByText('reconnect bar')).not.toBeInTheDocument();
  });

  it('opens a new shell for the restored server on reconnect', async () => {
    const user = userEvent.setup();
    restoreTerminalTabs([{ key: 'terminal-4', connectionId: 'web-id', connectionName: 'web' }], 'terminal-4');
    render(<TerminalWorkspace />);

    await user.click(screen.getByRole('button', { name: 'Reconnect' }));

    expect(screen.getByTestId('terminal-view')).toHaveAttribute('data-connection', 'web-id');
    expect(mocks.views[0]!.reconnect).toBe(false);
    expect(screen.getByRole('tab')).toHaveTextContent('Connecting…');
    expect(screen.getByRole('group', { name: 'web' })).toBeInTheDocument();
  });

  it('publishes restored tabs so they are saved again', () => {
    restoreTerminalTabs([{ key: 'terminal-4', connectionId: 'web-id', connectionName: 'web' }], 'terminal-4');

    render(<TerminalWorkspace />);

    const snapshot = getTerminalTabsSnapshot();
    expect(snapshot.tabs.map((tab) => [tab.connectionId, tab.connectionName])).toEqual([['web-id', 'web']]);
    expect(snapshot.activeKey).toBe(snapshot.tabs[0]!.key);
  });
});

import { act, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import type { SavedConnection } from '../api/connections';
import { replaceConnections } from '../state/connections';
import { selectServer } from '../state/selectedServer';
import { markWorkspaceSaved, resetWorkspaceSaved } from '../state/workspaceSaved';
import { AppFooter } from './AppFooter';

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

afterEach(() => {
  selectServer(null);
  resetWorkspaceSaved();
});

describe('AppFooter', () => {
  it('shows the selected server authentication, the transport and the last workspace save', () => {
    replaceConnections([CONNECTION]);
    selectServer('c1');
    render(<AppFooter />);

    act(() => markWorkspaceSaved(new Date(2026, 9, 25, 14, 5)));

    expect(screen.getByText('Auth: SSH agent')).toBeInTheDocument();
    expect(screen.getByText('Transport: SSH (PTY) + SFTP')).toBeInTheDocument();
    expect(screen.getByText('Workspace saved 14:05')).toBeInTheDocument();
  });

  it('stays quiet without a server or a save', () => {
    render(<AppFooter />);

    expect(screen.getByRole('contentinfo')).toHaveTextContent('');
  });
});

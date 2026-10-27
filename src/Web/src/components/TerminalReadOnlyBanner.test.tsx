import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SavedConnection } from '../api/connections';
import { replaceConnections } from '../state/connections';
import { TerminalReadOnlyBanner } from './TerminalReadOnlyBanner';

function connection(readOnly: boolean): SavedConnection {
  return {
    id: 'c1',
    name: 'db-01',
    groupName: null,
    host: '198.51.100.7',
    port: 22,
    userName: 'postgres',
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
    readOnly,
  };
}

describe('TerminalReadOnlyBanner', () => {
  it('warns that a read-only server shell still runs commands', () => {
    replaceConnections([connection(true)]);

    render(<TerminalReadOnlyBanner connectionId="c1" />);

    const banner = screen.getByRole('note');
    expect(banner).toHaveTextContent('db-01 is read-only.');
    expect(banner).toHaveTextContent('Commands typed in this shell still run');
  });

  it('renders nothing for a writable server', () => {
    replaceConnections([connection(false)]);

    render(<TerminalReadOnlyBanner connectionId="c1" />);

    expect(screen.queryByRole('note')).not.toBeInTheDocument();
  });
});

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { SavedConnection } from '../../api/connections';
import { getConnections, replaceConnections } from '../../state/connections';
import { ReadOnlyServersSection } from './ReadOnlyServersSection';

const mocks = vi.hoisted(() => ({
  listConnections: vi.fn(),
  updateConnection: vi.fn(),
}));

vi.mock('../../api/connections', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/connections')>()),
  listConnections: mocks.listConnections,
  updateConnection: mocks.updateConnection,
}));

function connection(overrides: Partial<SavedConnection>): SavedConnection {
  return {
    id: 'c1',
    name: 'web-01',
    groupName: 'Production',
    host: '203.0.113.24',
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
    aiAccess: false,
    ...overrides,
  };
}

beforeEach(() => {
  const servers = [connection({}), connection({ id: 'c2', name: 'db-01', readOnly: true })];
  mocks.listConnections.mockReset().mockResolvedValue(servers);
  mocks.updateConnection.mockReset();
  replaceConnections(servers);
});

describe('ReadOnlyServersSection', () => {
  it('lists every server with its read-only switch', () => {
    render(<ReadOnlyServersSection />);

    expect(screen.getByRole('switch', { name: 'web-01 read-only' })).toHaveAttribute('aria-checked', 'false');
    expect(screen.getByRole('switch', { name: 'db-01 read-only' })).toHaveAttribute('aria-checked', 'true');
    expect(screen.getAllByText('Production')).toHaveLength(2);
  });

  it('saves the flag and updates the shared server list', async () => {
    const user = userEvent.setup();
    mocks.updateConnection.mockResolvedValue(connection({ readOnly: true }));
    render(<ReadOnlyServersSection />);

    await user.click(screen.getByRole('switch', { name: 'web-01 read-only' }));

    await waitFor(() => expect(mocks.updateConnection).toHaveBeenCalledWith('c1', expect.objectContaining({ readOnly: true, host: '203.0.113.24' })));
    await waitFor(() => expect(getConnections().find((item) => item.id === 'c1')?.readOnly).toBe(true));
    expect(screen.getByRole('switch', { name: 'web-01 read-only' })).toHaveAttribute('aria-checked', 'true');
  });
});

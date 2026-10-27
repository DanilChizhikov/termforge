import { afterEach, describe, expect, it, vi } from 'vitest';
import * as api from '../api/connections';
import type { SavedConnection } from '../api/connections';
import { ensureConnectionsLoaded, findConnection, getConnections, reloadConnections, resetConnections } from './connections';

vi.mock('../api/connections', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api/connections')>();
  return { ...actual, listConnections: vi.fn() };
});

const listMock = vi.mocked(api.listConnections);

function makeConnection(id: string, name: string): SavedConnection {
  return {
    id,
    name,
    groupName: null,
    host: `${name}.example`,
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
  };
}

describe('connections store', () => {
  afterEach(() => {
    vi.clearAllMocks();
    resetConnections();
  });

  it('loads connections once and shares them', async () => {
    listMock.mockResolvedValue([makeConnection('c1', 'web')]);

    await ensureConnectionsLoaded();
    await ensureConnectionsLoaded();

    expect(listMock).toHaveBeenCalledTimes(1);
    expect(getConnections().map((connection) => connection.name)).toEqual(['web']);
    expect(findConnection('c1')?.name).toBe('web');
    expect(findConnection('missing')).toBeNull();
  });

  it('replaces connections on reload', async () => {
    listMock.mockResolvedValueOnce([makeConnection('c1', 'web')]);
    listMock.mockResolvedValueOnce([makeConnection('c2', 'db')]);

    await ensureConnectionsLoaded();
    await reloadConnections();

    expect(getConnections().map((connection) => connection.name)).toEqual(['db']);
  });
});

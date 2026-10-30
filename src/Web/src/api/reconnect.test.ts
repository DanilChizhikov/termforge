import { describe, expect, it, vi } from 'vitest';
import type { ApiClient } from './client';
import { inputFrom, emptyInput, type SavedConnection } from './connections';
import { parseServerMessage, reconnectTerminal } from './terminals';

describe('reconnect api', () => {
  it('posts the connection and size to the reconnect endpoint', async () => {
    const post = vi.fn().mockResolvedValue({ session: { id: 's' }, attempts: 2 });
    const client = { post } as unknown as ApiClient;

    const result = await reconnectTerminal('c1', { columns: 100, rows: 30 }, client);

    expect(post).toHaveBeenCalledWith('/api/terminals/reconnect', { connectionId: 'c1', columns: 100, rows: 30 });
    expect(result.attempts).toBe(2);
  });

  it('parses history change notifications', () => {
    expect(parseServerMessage('{"type":"history"}')).toEqual({ type: 'history' });
  });

  it('defaults the policy to manual reconnect and copies a stored policy into the form', () => {
    expect(emptyInput()).toMatchObject({ reconnectMode: 'Manual', reconnectMaxAttempts: 5 });

    const stored = {
      id: 'c1',
      name: 'web',
      groupName: null,
      host: 'h',
      port: 22,
      userName: 'u',
      authMethod: 'Agent',
      privateKeyPath: null,
      jumpHostId: null,
      sshAlias: null,
      hasPassword: false,
      lastConnectedAt: null,
      reconnectMode: 'Automatic',
      reconnectMaxAttempts: 9,
      reconnectInitialDelaySeconds: 3,
      readOnly: false,
      aiAccess: false,
      reconnectMaxDelaySeconds: 90,
    } satisfies SavedConnection;

    expect(inputFrom(stored)).toMatchObject({
      reconnectMode: 'Automatic',
      reconnectMaxAttempts: 9,
      reconnectInitialDelaySeconds: 3,
      reconnectMaxDelaySeconds: 90,
    });
  });
});

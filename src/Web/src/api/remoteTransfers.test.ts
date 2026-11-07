import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from './client';
import { startTransfers } from './transfers';

describe('server transfers', () => {
  it('requests overwrite confirmation for the destination server', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValueOnce(new Response(JSON.stringify({ token: 'grant' })))
      .mockResolvedValueOnce(new Response('[]', { status: 202 }));
    const client = new ApiClient({ fetchImpl });

    await startTransfers({ connectionId: 'source', targetConnectionId: 'target', direction: 'remoteToRemote', sourcePaths: ['/a'], targetDirectory: '/srv', overwrite: true }, client);

    expect(JSON.parse(fetchImpl.mock.calls[0]?.[1]?.body as string)).toEqual({ operation: 'TransferOverwrite', connectionId: 'target', target: '/srv' });
    expect(JSON.parse(fetchImpl.mock.calls[1]?.[1]?.body as string)).toMatchObject({ targetConnectionId: 'target', confirmationToken: 'grant', direction: 'remoteToRemote' });
  });

  it('preserves an explicit restart without granting overwrite', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(new Response('[]', { status: 202 }));
    const client = new ApiClient({ fetchImpl });

    await startTransfers({ connectionId: 'source', targetConnectionId: 'target', direction: 'remoteToRemote', restart: true, sourcePaths: ['/a'], targetDirectory: '/srv', overwrite: false }, client);

    expect(fetchImpl).toHaveBeenCalledTimes(1);
    expect(JSON.parse(fetchImpl.mock.calls[0]?.[1]?.body as string)).toMatchObject({ restart: true, overwrite: false });
  });
});

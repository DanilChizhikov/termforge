import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from './client';
import { describeAmount, describeRate, percentOf, startTransfers, type TransferRecord } from './transfers';

describe('server transfers', () => {
  it('reports content verification separately from copied bytes and ETA', () => {
    const transfer: TransferRecord = {
      id: 'check', connectionId: 'source', targetConnectionId: 'target', server: 'source', direction: 'remoteToRemote',
      sourcePath: '/a', targetPath: '/srv/a', status: 'running', transferredBytes: 0, totalBytes: 100,
      completedFiles: 0, totalFiles: 1, bytesPerSecond: 100, remainingSeconds: 1,
      error: null, retryable: false, partialBytes: 0, verification: { checkedBytes: 100, totalBytes: 200 },
    };

    expect(describeAmount(transfer)).toBe('Checking contents: 100 B of 200 B');
    expect(percentOf(transfer)).toBe(50);
    expect(describeRate(transfer)).toBe('');
  });
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

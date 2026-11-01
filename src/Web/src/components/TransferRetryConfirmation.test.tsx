import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { TransferPanel } from './TransferPanel';
import { replaceTransfers, resetTransfers } from '../state/transfers';
import type { TransferRecord } from '../api/transfers';

vi.mock('./useTransferFeed', () => ({ useTransferFeed: () => true }));

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

function transfer(direction: TransferRecord['direction']): TransferRecord {
  return {
    id: 'retry-1',
    connectionId: 'server-1',
    server: 'web-01',
    direction,
    sourcePath: '/source/a.txt',
    targetPath: '/target/a.txt',
    targetDirectory: '/target/',
    status: 'failed',
    transferredBytes: 5,
    totalBytes: 10,
    completedFiles: 0,
    totalFiles: 1,
    bytesPerSecond: 0,
    remainingSeconds: null,
    error: 'Disconnected',
    retryable: true,
    partialBytes: 5,
  };
}

beforeEach(() => resetTransfers());

afterEach(() => {
  resetTransfers();
  vi.unstubAllGlobals();
});

describe('transfer retry confirmation', () => {
  it.each(['upload', 'download'] as const)('asks again before overwriting on %s retry', async (direction) => {
    const retries: unknown[] = [];
    const confirmations: unknown[] = [];
    vi.stubGlobal('fetch', vi.fn((input: string, init?: RequestInit) => {
      const path = String(input);
      if (path === '/api/session') return Promise.resolve(json({ csrfToken: 'csrf' }));
      const body = typeof init?.body === 'string' ? JSON.parse(init.body) as unknown : undefined;
      if (path === '/api/confirmations') {
        confirmations.push(body);
        return Promise.resolve(json({ token: 'fresh-confirmation' }));
      }
      if (path === '/api/operations/retry-1/retry') {
        retries.push(body);
        return Promise.resolve(retries.length === 1
          ? json({ code: 'TransferConflict', conflicts: ['/target/a.txt'] }, 409)
          : new Response(null, { status: 202 }));
      }
      throw new Error(`Unexpected request: ${path}`);
    }));
    act(() => replaceTransfers([transfer(direction)]));
    const user = userEvent.setup();
    render(<TransferPanel />);

    await user.click(screen.getByRole('button', { name: 'Resume' }));
    await screen.findByRole('dialog');
    expect(retries).toEqual([{ overwrite: false }]);
    expect(confirmations).toEqual([]);
    await user.click(screen.getByRole('button', { name: 'Overwrite' }));

    await waitFor(() => expect(retries).toHaveLength(2));
    expect(confirmations).toEqual([{
      operation: direction === 'upload' ? 'TransferOverwrite' : 'LocalTransferOverwrite',
      connectionId: 'server-1',
      target: '/target/',
    }]);
    expect(retries[1]).toEqual({ overwrite: true, confirmationToken: 'fresh-confirmation' });
  });

  it('keeps the existing target when the retry confirmation is cancelled', async () => {
    const retries: unknown[] = [];
    vi.stubGlobal('fetch', vi.fn((input: string, init?: RequestInit) => {
      if (String(input) === '/api/session') return Promise.resolve(json({ csrfToken: 'csrf' }));
      retries.push(init?.body);
      return Promise.resolve(json({ code: 'TransferConflict', conflicts: ['/target/a.txt'] }, 409));
    }));
    act(() => replaceTransfers([transfer('upload')]));
    const user = userEvent.setup();
    render(<TransferPanel />);

    await user.click(screen.getByRole('button', { name: 'Resume' }));
    await screen.findByRole('dialog');
    await user.click(screen.getByRole('button', { name: 'Keep existing' }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(retries).toHaveLength(1);
  });
});

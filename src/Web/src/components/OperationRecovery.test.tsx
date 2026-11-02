import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { OperationRecord } from '../api/operations';
import { resetTransfers } from '../state/transfers';
import { OperationHistory } from './OperationHistory';

const operation: OperationRecord = {
  id: 'restored-1',
  kind: 'transfer.download',
  server: 'offline',
  status: 'failed',
  createdAt: '2026-11-01T10:00:00Z',
  startedAt: null,
  finishedAt: '2026-11-01T11:00:00Z',
  updatedAt: '2026-11-01T11:00:00Z',
  details: { connectionId: 'connection-1', source: '/remote/file', targetDirectory: '/local/password=[redacted]' },
  error: 'The operation was interrupted when the application stopped.',
  retryable: true,
  hasPartial: true,
};

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

let fetchMock: ReturnType<typeof vi.fn>;

beforeEach(() => {
  resetTransfers();
  vi.stubGlobal('WebSocket', class { close = vi.fn(); });
  fetchMock = vi.fn((input: string) => {
    const path = String(input);
    if (path === '/api/session') return Promise.resolve(json({ csrfToken: 'csrf' }));
    return Promise.resolve(json({ items: [operation], total: 1, offset: 0, limit: 20 }));
  });
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  resetTransfers();
  vi.unstubAllGlobals();
});

describe('recovered operations', () => {
  it('discards a tracked partial even when its byte count is unknown', async () => {
    const user = userEvent.setup();
    render(<OperationHistory />);
    await screen.findByRole('button', { name: 'Discard partial' });

    await user.click(screen.getByRole('button', { name: 'Discard partial' }));

    await waitFor(() => expect(fetchMock.mock.calls.some(([path]) => path === '/api/transfers/restored-1/discard')).toBe(true));
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Discard partial' })).not.toBeInTheDocument());
  });

  it('gets retry confirmation by id instead of using redacted display paths', async () => {
    let retried = false;
    const baseFetch = fetchMock.getMockImplementation() as (input: string) => Promise<Response>;
    fetchMock.mockImplementation((input: string) => {
      if (input === '/api/operations/restored-1/retry-confirmation') return Promise.resolve(json({ token: 'exact-scope-token' }));
      if (input === '/api/operations/restored-1/retry') {
        if (!retried) {
          retried = true;
          return Promise.resolve(json({ code: 'TransferConflict', conflicts: ['/local/file'] }, 409));
        }
        return Promise.resolve(new Response(null, { status: 202 }));
      }
      return baseFetch(input);
    });
    const user = userEvent.setup();
    render(<OperationHistory />);

    await user.click(await screen.findByRole('button', { name: 'Retry' }));
    const dialog = await screen.findByRole('dialog', { name: 'Replace existing item?' });
    await user.click(within(dialog).getByRole('button', { name: 'Overwrite' }));

    await waitFor(() => {
      const calls = fetchMock.mock.calls.filter(([path]) => path === '/api/operations/restored-1/retry');
      expect(calls).toHaveLength(2);
      expect(JSON.parse((calls[1]![1] as RequestInit).body as string)).toEqual({ overwrite: true, confirmationToken: 'exact-scope-token' });
    });
    expect(fetchMock.mock.calls.some(([path]) => path === '/api/operations/restored-1/retry-confirmation')).toBe(true);
    expect(fetchMock.mock.calls.some(([path]) => path === '/api/confirmations')).toBe(false);
  });
});

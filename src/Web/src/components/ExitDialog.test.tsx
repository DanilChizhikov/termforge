import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { PendingAttention } from '../api/lifecycle';
import type { TransferRecord } from '../api/transfers';
import { flushWorkspaceState } from '../state/workspacePersistence';
import { ExitDialog } from './ExitDialog';

vi.mock('../state/workspacePersistence', () => ({
  flushWorkspaceState: vi.fn(() => Promise.resolve()),
}));

interface RecordedCall {
  url: string;
  method: string;
  body: unknown;
}

const transfer: TransferRecord = {
  id: 't1',
  connectionId: 'c1',
  server: 'web-01',
  direction: 'upload',
  sourcePath: '/home/me/dist.tar.gz',
  targetPath: '/srv/dist.tar.gz',
  status: 'running',
  transferredBytes: 62,
  totalBytes: 100,
  completedFiles: 0,
  totalFiles: 1,
  bytesPerSecond: 10,
  remainingSeconds: 4,
  error: null,
  retryable: false,
  partialBytes: 0,
};

function attention(overrides: Partial<PendingAttention> = {}): PendingAttention {
  return {
    state: 'running',
    transfers: [],
    unsavedFiles: [],
    terminalSessions: [],
    idleShutdownMinutes: 0,
    ...overrides,
  };
}

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status });
}

type Handler = (call: RecordedCall) => Response | Promise<Response>;

function stubBackend(handler: Handler): RecordedCall[] {
  const calls: RecordedCall[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = init?.method ?? 'GET';
    if (url === '/api/session') {
      return json({ csrfToken: 'csrf' });
    }
    const body = typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined;
    const call = { url, method, body };
    calls.push(call);
    return handler(call);
  });
  vi.stubGlobal('fetch', fetchMock);
  return calls;
}

beforeEach(() => {
  vi.mocked(flushWorkspaceState).mockClear();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('ExitDialog', () => {
  it('lists active transfers, unsaved files and terminal sessions', async () => {
    stubBackend(() =>
      json(
        attention({
          transfers: [transfer],
          unsavedFiles: [{ connectionId: 'c1', server: 'web-01', path: '/etc/nginx/site.conf' }],
          terminalSessions: [
            { connectionId: 'c1', server: 'web-01', sessionNumber: 1, openedAt: '2026-10-27T10:00:00Z' },
            { connectionId: 'c1', server: 'web-01', sessionNumber: 2, openedAt: '2026-10-27T10:01:00Z' },
          ],
        }),
      ),
    );

    render(<ExitDialog onClose={() => undefined} />);

    expect(await screen.findByText('1 transfer in progress')).toBeInTheDocument();
    expect(screen.getByText('This stops the local backend. Three things need your attention first.')).toBeInTheDocument();
    expect(screen.getByText('dist.tar.gz → web-01')).toBeInTheDocument();
    expect(screen.getByRole('progressbar', { name: 'dist.tar.gz → web-01 progress' })).toHaveAttribute('value', '62');
    expect(screen.getByText('1 unsaved file')).toBeInTheDocument();
    expect(screen.getByText('site.conf')).toBeInTheDocument();
    expect(screen.getByText('2 terminal sessions')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Exit after transfers finish' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Exit now' })).toBeInTheDocument();
    expect(flushWorkspaceState).toHaveBeenCalled();
  });

  it('exits immediately when nothing needs attention', async () => {
    const calls = stubBackend((call) =>
      call.method === 'POST' ? json({ state: 'shuttingDown' }, 202) : json(attention()),
    );

    render(<ExitDialog onClose={() => undefined} pollIntervalMs={20} />);
    expect(await screen.findByText('This stops the local backend. Nothing needs your attention.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Exit' }));

    expect(await screen.findByText('Exiting TermForge…')).toBeInTheDocument();
    const exitCall = calls.find((call) => call.method === 'POST');
    expect(exitCall).toEqual({ url: '/api/lifecycle/exit', method: 'POST', body: { waitForTransfers: false } });
    expect(flushWorkspaceState).toHaveBeenCalledTimes(2);
  });

  it('waits for transfers and reports when the backend has stopped', async () => {
    let stopped = false;
    const calls = stubBackend((call) => {
      if (call.method === 'POST') {
        return json({ state: 'waitingForTransfers' }, 202);
      }
      if (stopped) {
        throw new TypeError('Failed to fetch');
      }
      return json(attention({ transfers: [transfer] }));
    });

    render(<ExitDialog onClose={() => undefined} pollIntervalMs={20} />);
    await userEvent.click(await screen.findByRole('button', { name: 'Exit after transfers finish' }));

    expect(await screen.findByText('Waiting for transfers')).toBeInTheDocument();
    expect(calls.find((call) => call.method === 'POST')?.body).toEqual({ waitForTransfers: true });
    stopped = true;
    expect(await screen.findByText('TermForge has stopped')).toBeInTheDocument();
  });

  it('cancels a pending exit when the user keeps working', async () => {
    const onClose = vi.fn();
    const calls = stubBackend((call) => {
      if (call.method === 'POST') {
        return json({ state: 'waitingForTransfers' }, 202);
      }
      if (call.method === 'DELETE') {
        return new Response(null, { status: 204 });
      }
      return json(attention({ transfers: [transfer] }));
    });

    render(<ExitDialog onClose={onClose} pollIntervalMs={10000} />);
    await userEvent.click(await screen.findByRole('button', { name: 'Exit after transfers finish' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Keep working' }));

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(calls.some((call) => call.method === 'DELETE' && call.url === '/api/lifecycle/exit')).toBe(true);
  });

  it('closes without exiting on cancel', async () => {
    const onClose = vi.fn();
    const calls = stubBackend(() => json(attention()));

    render(<ExitDialog onClose={onClose} />);
    await userEvent.click(await screen.findByRole('button', { name: 'Cancel' }));

    expect(onClose).toHaveBeenCalled();
    expect(calls.some((call) => call.method === 'POST')).toBe(false);
  });

  it('mentions idle shutdown when it is enabled', async () => {
    stubBackend(() => json(attention({ idleShutdownMinutes: 30 })));

    render(<ExitDialog onClose={() => undefined} />);

    expect(await screen.findByText(/stops after 30 minutes without an open tab/)).toBeInTheDocument();
  });
});

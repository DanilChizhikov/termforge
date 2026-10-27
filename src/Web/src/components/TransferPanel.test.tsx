import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { TransferRecord } from '../api/transfers';
import { requestTransfers, resetTransfers } from '../state/transfers';
import { TransferPanel } from './TransferPanel';

function makeTransfer(overrides: Partial<TransferRecord> = {}): TransferRecord {
  return {
    id: 't-1',
    connectionId: 'c-1',
    server: 'web-01',
    direction: 'upload',
    sourcePath: '/home/me/dist.tar.gz',
    targetPath: '/var/www/site/dist.tar.gz',
    status: 'running',
    transferredBytes: 512,
    totalBytes: 1024,
    completedFiles: 0,
    totalFiles: 1,
    bytesPerSecond: 256,
    remainingSeconds: 2,
    error: null,
    retryable: false,
    partialBytes: 0,
    ...overrides,
  };
}

class FakeSocket {
  static instances: FakeSocket[] = [];
  onopen: (() => void) | null = null;
  onclose: (() => void) | null = null;
  onmessage: ((event: MessageEvent<string>) => void) | null = null;
  close = vi.fn();

  constructor(public readonly url: string) {
    FakeSocket.instances.push(this);
  }

  emit(data: unknown): void {
    this.onmessage?.({ data: JSON.stringify(data) } as MessageEvent<string>);
  }
}

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

let fetchMock: ReturnType<typeof vi.fn>;
let listed: TransferRecord[];
let startResponses: Response[];

function postedBodies(path: string): unknown[] {
  return fetchMock.mock.calls
    .filter(([input, init]) => String(input) === path && (init as RequestInit | undefined)?.method === 'POST')
    .map(([, init]) => {
      const body = (init as RequestInit).body;
      return typeof body === 'string' ? (JSON.parse(body) as unknown) : undefined;
    });
}

beforeEach(() => {
  resetTransfers();
  FakeSocket.instances = [];
  listed = [];
  startResponses = [];
  fetchMock = vi.fn((input: string, init?: RequestInit) => {
    const path = String(input);
    if (path === '/api/session') {
      return Promise.resolve(json({ csrfToken: 'csrf' }));
    }
    if (path === '/api/confirmations') {
      return Promise.resolve(json({ token: 'confirm-1' }));
    }
    if (path === '/api/transfers' && init?.method === 'POST') {
      return Promise.resolve(startResponses.shift() ?? json([]));
    }
    if (path === '/api/transfers') {
      return Promise.resolve(json(listed));
    }
    return Promise.resolve(new Response(null, { status: 204 }));
  });
  vi.stubGlobal('fetch', fetchMock);
  vi.stubGlobal('WebSocket', FakeSocket);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('TransferPanel', () => {
  it('shows an empty hint when nothing is queued', async () => {
    render(<TransferPanel />);

    expect(await screen.findByText(/drag them between the local and remote panes/)).toBeInTheDocument();
    expect(screen.getByText('0 active · 0 queued · 0 failed')).toBeInTheDocument();
  });

  it('renders progress, rate and remaining time of a running transfer', async () => {
    listed = [makeTransfer()];

    render(<TransferPanel />);

    const card = await screen.findByRole('listitem', { name: 'dist.tar.gz → /var/www/site/' });
    expect(within(card).getByText('Running')).toBeInTheDocument();
    expect(within(card).getByText('512 B of 1.0 KB · 50%')).toBeInTheDocument();
    expect(within(card).getByText('256 B/s · 2 s left')).toBeInTheDocument();
    expect(within(card).getByRole('progressbar', { name: 'dist.tar.gz progress' })).toHaveAttribute('value', '50');
  });

  it('applies live progress events from the socket', async () => {
    listed = [makeTransfer()];
    render(<TransferPanel />);
    await screen.findByText('512 B of 1.0 KB · 50%');

    const socket = FakeSocket.instances[0];
    act(() => {
      socket?.onopen?.();
      socket?.emit({ type: 'transfer.changed', data: makeTransfer({ status: 'succeeded', transferredBytes: 1024 }) });
    });

    expect(await screen.findByText('Done')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Cancel' })).not.toBeInTheDocument();
  });

  it('removes a transfer when the server reports it removed', async () => {
    listed = [makeTransfer()];
    render(<TransferPanel />);
    await screen.findByText('Running');

    act(() => {
      FakeSocket.instances[0]?.emit({ type: 'transfer.removed', data: { id: 't-1' } });
    });

    expect(await screen.findByText(/drag them between the local and remote panes/)).toBeInTheDocument();
  });

  it('cancels an active transfer', async () => {
    listed = [makeTransfer()];
    const user = userEvent.setup();
    render(<TransferPanel />);

    await user.click(await screen.findByRole('button', { name: 'Cancel' }));

    await waitFor(() => expect(postedBodies('/api/transfers/t-1/cancel')).toHaveLength(1));
  });

  it('offers resume for a retryable failure with a partial file', async () => {
    listed = [
      makeTransfer({ status: 'failed', retryable: true, partialBytes: 300, error: 'Connection reset by peer' }),
    ];
    const user = userEvent.setup();
    render(<TransferPanel />);

    expect(await screen.findByText('Connection reset by peer')).toBeInTheDocument();
    expect(screen.getByText('resumable from 300 B')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Resume' }));

    await waitFor(() => expect(postedBodies('/api/operations/t-1/retry')).toHaveLength(1));
  });

  it('offers retry without resume when no partial file is kept', async () => {
    listed = [makeTransfer({ status: 'failed', retryable: true, error: 'Timed out' })];

    render(<TransferPanel />);

    expect(await screen.findByRole('button', { name: 'Retry' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Discard partial' })).not.toBeInTheDocument();
  });

  it('asks before overwriting and resends the request with overwrite confirmed', async () => {
    startResponses = [json({ code: 'TransferConflict', conflicts: ['/var/www/site/dist.tar.gz'] }, 409), json([])];
    const user = userEvent.setup();
    render(<TransferPanel />);
    await screen.findByText(/drag them between/);

    const request = {
      connectionId: 'c-1',
      direction: 'upload' as const,
      sourcePaths: ['/home/me/dist.tar.gz'],
      targetDirectory: '/var/www/site',
      overwrite: false,
    };
    await act(() => requestTransfers(request));

    const dialog = await screen.findByRole('dialog', { name: 'Replace existing item?' });
    expect(within(dialog).getByText('/var/www/site/dist.tar.gz')).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Keep existing' })).toHaveFocus();
    await user.click(within(dialog).getByRole('button', { name: 'Overwrite' }));

    await waitFor(() =>
      expect(postedBodies('/api/transfers')).toEqual([request, { ...request, overwrite: true, confirmationToken: 'confirm-1' }]),
    );
    expect(postedBodies('/api/confirmations')).toEqual([
      { operation: 'TransferOverwrite', connectionId: 'c-1', target: '/var/www/site' },
    ]);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('keeps the existing target when the conflict is declined', async () => {
    startResponses = [json({ code: 'TransferConflict', conflicts: ['/a', '/b'] }, 409)];
    const user = userEvent.setup();
    render(<TransferPanel />);
    await screen.findByText(/drag them between/);

    const request = {
      connectionId: 'c-1',
      direction: 'download' as const,
      sourcePaths: ['/a', '/b'],
      targetDirectory: '/home/me',
      overwrite: false,
    };
    await act(() => requestTransfers(request));
    await user.click(await screen.findByRole('button', { name: 'Keep existing' }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(postedBodies('/api/transfers')).toHaveLength(1);
  });
});

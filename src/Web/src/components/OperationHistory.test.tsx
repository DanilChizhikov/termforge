import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { OperationPage, OperationRecord } from '../api/operations';
import type { SavedConnection } from '../api/connections';
import { replaceConnections } from '../state/connections';
import { mergeOperation, OperationHistory } from './OperationHistory';

function makeConnection(name: string): SavedConnection {
  return {
    id: `id-${name}`,
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
    readOnly: false,
  };
}

function makeOperation(overrides: Partial<OperationRecord> = {}): OperationRecord {
  return {
    id: 'op-1',
    kind: 'transfer',
    server: 'web-1',
    status: 'failed',
    createdAt: '2026-10-25T10:00:00Z',
    startedAt: null,
    finishedAt: null,
    updatedAt: '2026-10-25T10:00:00Z',
    details: {},
    error: 'boom',
    retryable: true,
    ...overrides,
  };
}

function pageOf(items: OperationRecord[]): OperationPage {
  return { items, total: items.length, offset: 0, limit: 20 };
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

let fetchMock: ReturnType<typeof vi.fn>;

function respondWith(page: OperationPage): void {
  fetchMock.mockImplementation((input: string, init?: RequestInit) => {
    if (init?.method === 'POST' && String(input).includes('/retry')) {
      return Promise.resolve(new Response(null, { status: 202 }));
    }
    return Promise.resolve(new Response(JSON.stringify(page), { status: 200 }));
  });
}

beforeEach(() => {
  FakeSocket.instances = [];
  fetchMock = vi.fn();
  vi.stubGlobal('fetch', fetchMock);
  vi.stubGlobal('WebSocket', FakeSocket);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('OperationHistory', () => {
  it('renders the list returned by the api', async () => {
    respondWith(pageOf([makeOperation()]));

    render(<OperationHistory />);

    expect(await screen.findByText('transfer')).toBeInTheDocument();
    expect(screen.getByText('web-1')).toBeInTheDocument();
  });

  it('applies filters to the api as soon as they change', async () => {
    respondWith(pageOf([]));
    replaceConnections([makeConnection('web-2')]);
    const user = userEvent.setup();
    render(<OperationHistory />);

    await user.click(screen.getByRole('button', { name: 'Docker' }));
    await user.selectOptions(screen.getByLabelText('Status'), 'failed');
    await user.selectOptions(screen.getByLabelText('Server'), 'web-2');

    await waitFor(() => {
      const lastUrl = String(fetchMock.mock.calls.at(-1)?.[0]);
      expect(lastUrl).toContain('kind=docker');
      expect(lastUrl).toContain('status=failed');
      expect(lastUrl).toContain('server=web-2');
      expect(lastUrl).toContain('from=');
    });
    expect(screen.getByRole('button', { name: 'Docker' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('drops the period filter for all time', async () => {
    respondWith(pageOf([]));
    const user = userEvent.setup();
    render(<OperationHistory />);

    await user.selectOptions(screen.getByLabelText('Period'), 'all');

    await waitFor(() => expect(String(fetchMock.mock.calls.at(-1)?.[0])).not.toContain('from='));
    expect(screen.getByText('0 operations in total')).toBeInTheDocument();
  });

  it('describes operations in words', async () => {
    respondWith(
      pageOf([
        makeOperation({
          id: 'up',
          kind: 'transfer.upload',
          status: 'succeeded',
          error: null,
          startedAt: '2026-10-25T10:00:00Z',
          finishedAt: '2026-10-25T10:00:04Z',
          details: { source: '/home/me/dist.tar.gz', targetDirectory: '/var/www/site' },
        }),
        makeOperation({ id: 'stop', kind: 'docker.stop', status: 'cancelled', error: null, retryable: false, details: { container: 'redis', action: 'stop' } }),
      ]),
    );

    render(<OperationHistory />);

    expect(await screen.findByText('Upload')).toBeInTheDocument();
    expect(screen.getByText('dist.tar.gz → /var/www/site/')).toBeInTheDocument();
    expect(screen.getByTestId('status-up')).toHaveTextContent('Done · 4 s');
    expect(screen.getByText('Stop redis')).toBeInTheDocument();
    expect(screen.getByTestId('status-stop')).toHaveTextContent('Cancelled');
  });

  it('shows Retry only for retryable failed operations', async () => {
    respondWith(
      pageOf([
        makeOperation({ id: 'a', kind: 'retryable-one' }),
        makeOperation({ id: 'b', kind: 'plain-one', retryable: false }),
        makeOperation({ id: 'c', kind: 'running-one', status: 'running' }),
      ]),
    );

    render(<OperationHistory />);
    await screen.findByText('retryable-one');

    expect(screen.getAllByRole('button', { name: 'Retry' })).toHaveLength(1);
  });

  it('posts a retry request when Retry is clicked', async () => {
    respondWith(pageOf([makeOperation()]));
    const user = userEvent.setup();
    render(<OperationHistory />);
    await screen.findByText('transfer');
    fetchMock.mockClear();
    fetchMock.mockImplementation((input: string) =>
      Promise.resolve(
        String(input) === '/api/session'
          ? new Response(JSON.stringify({ csrfToken: 't' }), { status: 200 })
          : new Response(null, { status: 202 }),
      ),
    );

    await user.click(screen.getByRole('button', { name: 'Retry' }));

    await waitFor(() => {
      const urls = fetchMock.mock.calls.map((call) => String(call[0]));
      expect(urls).toContain('/api/operations/op-1/retry');
    });
  });

  it('shows the server error when retry is not supported', async () => {
    respondWith(pageOf([makeOperation()]));
    const user = userEvent.setup();
    render(<OperationHistory />);
    await screen.findByText('transfer');
    fetchMock.mockImplementation((input: string) =>
      Promise.resolve(
        String(input) === '/api/session'
          ? new Response(JSON.stringify({ csrfToken: 't' }), { status: 200 })
          : new Response(JSON.stringify({ detail: 'no retry handler is registered' }), { status: 501 }),
      ),
    );

    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('no retry handler is registered');
  });

  it('updates the status live from the realtime channel', async () => {
    respondWith(pageOf([makeOperation({ status: 'running', retryable: false })]));
    render(<OperationHistory />);
    await screen.findByTestId('status-op-1');

    act(() => {
      FakeSocket.instances[0]!.emit({
        type: 'operation.changed',
        data: makeOperation({ status: 'succeeded', retryable: false }),
      });
    });

    expect(screen.getByTestId('status-op-1')).toHaveTextContent('Done');
  });
});

describe('mergeOperation', () => {
  const noFilters = { kind: '', status: '' as const, server: '', from: '', to: '' };

  it('prepends new operations matching the filters on the first page', () => {
    const merged = mergeOperation(pageOf([makeOperation()]), makeOperation({ id: 'new' }), noFilters);

    expect(merged.items.map((item) => item.id)).toEqual(['new', 'op-1']);
    expect(merged.total).toBe(2);
  });

  it('ignores new operations that do not match the filters', () => {
    const filters = { ...noFilters, kind: 'docker' };

    const merged = mergeOperation(pageOf([]), makeOperation({ id: 'new' }), filters);

    expect(merged.items).toHaveLength(0);
  });
});

import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { OperationPage, OperationRecord } from '../api/operations';
import { mergeOperation, OperationHistory } from './OperationHistory';

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

  it('sends filters to the api when applied', async () => {
    respondWith(pageOf([]));
    const user = userEvent.setup();
    render(<OperationHistory />);

    await user.type(screen.getByLabelText('Kind'), 'docker');
    await user.selectOptions(screen.getByLabelText('Status'), 'failed');
    await user.type(screen.getByLabelText('Server'), 'web-2');
    await user.click(screen.getByRole('button', { name: 'Apply' }));

    await waitFor(() => {
      const lastUrl = String(fetchMock.mock.calls.at(-1)?.[0]);
      expect(lastUrl).toContain('kind=docker');
      expect(lastUrl).toContain('status=failed');
      expect(lastUrl).toContain('server=web-2');
    });
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

    expect(screen.getByTestId('status-op-1')).toHaveTextContent('succeeded');
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

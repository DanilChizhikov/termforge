import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { CapabilityReport } from '../api/diagnostics';
import { selectServer } from '../state/selectedServer';
import { ServerDiagnostics } from './ServerDiagnostics';

function makeReport(overrides: Partial<CapabilityReport> = {}): CapabilityReport {
  return {
    connectionId: 'srv-1',
    state: 'Completed',
    checkedAt: '2026-10-26T10:00:00Z',
    message: null,
    capabilities: [
      { id: 'sftp', name: 'SFTP file transfer', status: 'Available', reason: 'The SFTP subsystem is enabled.' },
      {
        id: 'docker.access',
        name: 'Docker access',
        status: 'Unavailable',
        reason: 'Permission denied: the user is not in the docker group.',
      },
    ],
    ...overrides,
  };
}

function stubFetch(handler: (url: string, method: string) => Response) {
  const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) =>
    Promise.resolve(handler(String(input), init?.method ?? 'GET')),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function json(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200 });
}

afterEach(() => {
  vi.unstubAllGlobals();
  act(() => selectServer(null));
});

describe('ServerDiagnostics', () => {
  it('asks to select a server first', () => {
    const fetchMock = stubFetch(() => json(makeReport()));

    render(<ServerDiagnostics />);

    expect(screen.getByText(/Select a server/)).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('shows each capability with its status and reason', async () => {
    act(() => selectServer('srv-1'));
    stubFetch(() => json(makeReport()));

    render(<ServerDiagnostics />);

    expect(await screen.findByText('SFTP file transfer')).toBeInTheDocument();
    expect(screen.getByText(/not in the docker group/)).toBeInTheDocument();
    expect(screen.getByText('Unavailable')).toBeInTheDocument();
  });

  it('refreshes through the refresh endpoint', async () => {
    act(() => selectServer('srv-1'));
    const user = userEvent.setup();
    const fetchMock = stubFetch((url, method) => {
      if (method === 'POST' && url.endsWith('/refresh')) {
        return json(makeReport({ capabilities: [] , state: 'ConnectionFailed', message: 'no route to host' }));
      }
      if (url.endsWith('/api/session')) {
        return json({ csrfToken: 'token' });
      }
      return json(makeReport());
    });

    render(<ServerDiagnostics />);
    await screen.findByText('SFTP file transfer');
    await user.click(screen.getByRole('button', { name: 'Refresh' }));

    expect(await screen.findByText('no route to host')).toBeInTheDocument();
    const refreshCalls = fetchMock.mock.calls.filter(([url]) => String(url).endsWith('/refresh'));
    expect(refreshCalls).toHaveLength(1);
  });

  it('shows an error when loading fails', async () => {
    act(() => selectServer('srv-1'));
    stubFetch(() => new Response(null, { status: 500 }));

    render(<ServerDiagnostics />);

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load diagnostics.');
  });
});

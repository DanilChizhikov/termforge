import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { UpdateStatus } from '../api/updates';
import { UpdateNotice } from './UpdateNotice';

function makeStatus(overrides: Partial<UpdateStatus> = {}): UpdateStatus {
  return {
    state: 'uptodate',
    currentVersion: '0.1.0',
    latestVersion: '0.1.0',
    releaseUrl: null,
    checkedAt: '2026-10-25T10:00:00Z',
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
});

describe('UpdateNotice', () => {
  it('shows version and a download link when an update is available', async () => {
    const releaseUrl = 'https://github.com/DanilChizhikov/termforge/releases/tag/v0.2.0';
    stubFetch(() => json(makeStatus({ state: 'updateavailable', latestVersion: '0.2.0', releaseUrl })));

    render(<UpdateNotice />);

    expect(await screen.findByText(/Update available: 0.2.0/)).toBeInTheDocument();
    expect(screen.getByText(/TermForge 0.1.0/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Download' })).toHaveAttribute('href', releaseUrl);
  });

  it('shows up to date without a link', async () => {
    stubFetch(() => json(makeStatus()));

    render(<UpdateNotice />);

    expect(await screen.findByText(/Up to date/)).toBeInTheDocument();
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });

  it('renders nothing when the status request fails', async () => {
    const fetchMock = stubFetch(() => new Response(null, { status: 500 }));

    render(<UpdateNotice />);

    await vi.waitFor(() => expect(fetchMock).toHaveBeenCalled());
    expect(screen.queryByTestId('update-notice')).not.toBeInTheDocument();
  });

  it('checks on demand and shows the new result', async () => {
    const user = userEvent.setup();
    const releaseUrl = 'https://github.com/DanilChizhikov/termforge/releases/tag/v0.3.0';
    stubFetch((url, method) => {
      if (url === '/api/session') {
        return json({ csrfToken: 'token' });
      }
      if (method === 'POST') {
        return json(makeStatus({ state: 'updateavailable', latestVersion: '0.3.0', releaseUrl }));
      }
      return json(makeStatus({ state: 'notchecked', latestVersion: null }));
    });

    render(<UpdateNotice />);
    await user.click(await screen.findByRole('button', { name: 'Check for updates' }));

    expect(await screen.findByText(/Update available: 0.3.0/)).toBeInTheDocument();
  });

  it('reports an unavailable check quietly', async () => {
    stubFetch(() => json(makeStatus({ state: 'checkfailed', latestVersion: null })));

    render(<UpdateNotice />);

    expect(await screen.findByText(/Update check unavailable/)).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});

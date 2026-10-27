import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { UpdateStatus } from '../api/updates';
import { useAppView } from '../state/appView';
import { resetUpdates } from '../state/updates';
import { UpdatesCard } from './settings/UpdatesCard';
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

function ViewProbe() {
  return <span data-testid="view">{useAppView()}</span>;
}

afterEach(() => {
  vi.unstubAllGlobals();
  resetUpdates();
});

describe('UpdateNotice', () => {
  it('shows a pill that opens settings when an update is available', async () => {
    const user = userEvent.setup();
    const releaseUrl = 'https://github.com/DanilChizhikov/termforge/releases/tag/v0.2.0';
    stubFetch(() => json(makeStatus({ state: 'updateavailable', latestVersion: '0.2.0', releaseUrl })));

    render(
      <>
        <UpdateNotice />
        <ViewProbe />
      </>,
    );
    await user.click(await screen.findByRole('button', { name: 'Update v0.2.0 available' }));

    expect(screen.getByTestId('view')).toHaveTextContent('settings');
  });

  it('renders nothing when up to date', async () => {
    const fetchMock = stubFetch(() => json(makeStatus()));

    render(<UpdateNotice />);

    await vi.waitFor(() => expect(fetchMock).toHaveBeenCalled());
    expect(screen.queryByTestId('update-notice')).not.toBeInTheDocument();
  });

  it('renders nothing when the status request fails', async () => {
    const fetchMock = stubFetch(() => new Response(null, { status: 500 }));

    render(<UpdateNotice />);

    await vi.waitFor(() => expect(fetchMock).toHaveBeenCalled());
    expect(screen.queryByTestId('update-notice')).not.toBeInTheDocument();
  });
});

describe('UpdatesCard', () => {
  it('shows versions and the release page when an update is available', async () => {
    const releaseUrl = 'https://github.com/DanilChizhikov/termforge/releases/tag/v0.2.0';
    stubFetch(() => json(makeStatus({ state: 'updateavailable', latestVersion: '0.2.0', releaseUrl })));

    render(<UpdatesCard />);

    expect(await screen.findByText(/v0.2.0 is available/)).toBeInTheDocument();
    expect(screen.getByText(/you have v0.1.0/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Open the release page' })).toHaveAttribute('href', releaseUrl);
  });

  it('shows up to date without a link', async () => {
    stubFetch(() => json(makeStatus()));

    render(<UpdatesCard />);

    expect(await screen.findByText(/Up to date/)).toBeInTheDocument();
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
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

    render(<UpdatesCard />);
    await user.click(await screen.findByRole('button', { name: 'Check for updates' }));

    expect(await screen.findByText(/v0.3.0 is available/)).toBeInTheDocument();
  });

  it('reports an unavailable check quietly', async () => {
    stubFetch(() => json(makeStatus({ state: 'checkfailed', latestVersion: null })));

    render(<UpdatesCard />);

    expect(await screen.findByText(/Update check unavailable/)).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});

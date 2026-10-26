import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { isInsecureNetworkConnection } from '../api/session';
import { LoginForm } from './LoginForm';
import { SessionBanner } from './SessionBanner';

function stubFetch(handler: (url: string, method: string, body: string | undefined) => Response) {
  const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) =>
    Promise.resolve(handler(String(input), init?.method ?? 'GET', init?.body as string | undefined)),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('LoginForm', () => {
  it('signs in with the password and notifies the caller', async () => {
    const user = userEvent.setup();
    const fetchMock = stubFetch(() => new Response(null, { status: 204 }));
    const onSignedIn = vi.fn();
    render(<LoginForm onSignedIn={onSignedIn} />);

    await user.type(screen.getByLabelText('Password'), 'secret password');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    await vi.waitFor(() => expect(onSignedIn).toHaveBeenCalledOnce());
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/session/login',
      expect.objectContaining({ method: 'POST', body: JSON.stringify({ password: 'secret password' }) }),
    );
  });

  it('shows an error for a wrong password', async () => {
    const user = userEvent.setup();
    stubFetch(() => new Response('Wrong password', { status: 401 }));
    const onSignedIn = vi.fn();
    render(<LoginForm onSignedIn={onSignedIn} />);

    await user.type(screen.getByLabelText('Password'), 'wrong');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Wrong password.');
    expect(onSignedIn).not.toHaveBeenCalled();
  });

  it('shows the lockout delay', async () => {
    const user = userEvent.setup();
    stubFetch(() => new Response('locked', { status: 429, headers: { 'Retry-After': '900' } }));
    render(<LoginForm onSignedIn={vi.fn()} />);

    await user.type(screen.getByLabelText('Password'), 'wrong');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Try again in 900 seconds');
  });
});

describe('isInsecureNetworkConnection', () => {
  it.each([
    ['http:', '192.168.1.10', true],
    ['http:', 'termforge.lan', true],
    ['https:', 'termforge.lan', false],
    ['http:', 'localhost', false],
    ['http:', '127.0.0.1', false],
    ['http:', '[::1]', false],
  ])('%s//%s insecure=%s', (protocol, hostname, expected) => {
    expect(isInsecureNetworkConnection({ protocol, hostname })).toBe(expected);
  });
});

describe('SessionBanner', () => {
  it('offers password sign-in when the session is missing in network mode', async () => {
    stubFetch((url) =>
      url === '/api/session/login'
        ? new Response(JSON.stringify({ passwordLoginEnabled: true }), { status: 200 })
        : new Response(null, { status: 401 }),
    );

    render(<SessionBanner />);

    expect(await screen.findByRole('form', { name: 'Sign in' })).toBeInTheDocument();
    expect(screen.queryByText(/Restart TermForge/)).not.toBeInTheDocument();
  });

  it('asks to restart when password sign-in is disabled', async () => {
    stubFetch((url) =>
      url === '/api/session/login'
        ? new Response(JSON.stringify({ passwordLoginEnabled: false }), { status: 200 })
        : new Response(null, { status: 401 }),
    );

    render(<SessionBanner />);

    expect(await screen.findByRole('alert')).toHaveTextContent(/Restart TermForge/);
    expect(screen.queryByRole('form', { name: 'Sign in' })).not.toBeInTheDocument();
  });

  it('offers sign out for a password session', async () => {
    const user = userEvent.setup();
    const fetchMock = stubFetch((url, method) => {
      if (url === '/api/session') {
        return new Response(JSON.stringify({ csrfToken: 'csrf' }), { status: 200 });
      }
      if (url === '/api/session/login') {
        return new Response(JSON.stringify({ passwordLoginEnabled: true, signedInWithPassword: true }), { status: 200 });
      }
      if (url === '/api/session/logout' && method === 'POST') {
        return new Response(null, { status: 204 });
      }
      return new Response(null, { status: 404 });
    });

    render(<SessionBanner />);
    await user.click(await screen.findByRole('button', { name: 'Sign out' }));

    await vi.waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith('/api/session/logout', expect.objectContaining({ method: 'POST' })),
    );
  });
});

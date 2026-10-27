import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { PendingHostKey } from '../api/hostKeys';
import { HostKeyDialog } from './HostKeyDialog';
import { HostKeyPrompt } from './HostKeyPrompt';

const PRESENTED = { algorithm: 'ssh-ed25519', fingerprint: 'SHA256:presentedKeyFingerprint' };
const STALE = { algorithm: 'ssh-ed25519', fingerprint: 'SHA256:staleKeyFingerprint' };

function makePending(overrides: Partial<PendingHostKey> = {}): PendingHostKey {
  return {
    id: 'pending-1',
    host: 'web-1.example',
    port: 2222,
    status: 'unknown',
    presentedKey: PRESENTED,
    knownKeys: [],
    expiresAt: '2026-10-25T10:02:00Z',
    ...overrides,
  };
}

describe('HostKeyDialog', () => {
  it('shows endpoint, algorithm and fingerprint of an unknown key in a modal', () => {
    render(<HostKeyDialog pending={makePending()} error={null} busy={false} onDecide={vi.fn()} />);

    const dialog = screen.getByRole('dialog', { name: /Unknown host key for web-1.example:2222/ });
    expect(dialog).toHaveAttribute('aria-modal', 'true');
    expect(screen.getByText('ED25519')).toBeInTheDocument();
    expect(screen.getByText(PRESENTED.fingerprint)).toBeInTheDocument();
    expect(screen.getByText('ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub')).toBeInTheDocument();
  });

  it.each([
    ['Accept once', 'acceptOnce'],
    ['Accept and save', 'acceptAndSave'],
    ['Reject', 'reject'],
  ])('sends %s for an unknown key', async (label, decision) => {
    const onDecide = vi.fn();
    const user = userEvent.setup();
    render(<HostKeyDialog pending={makePending()} error={null} busy={false} onDecide={onDecide} />);

    await user.click(screen.getByRole('button', { name: label }));

    expect(onDecide).toHaveBeenCalledWith(decision, false);
  });

  it('blocks a changed key with a warning and offers no accept', () => {
    const pending = makePending({ status: 'changed', knownKeys: [STALE] });

    render(<HostKeyDialog pending={pending} error={null} busy={false} onDecide={vi.fn()} />);

    expect(screen.getByRole('alertdialog', { name: 'The host key for web-1.example has changed' })).toBeInTheDocument();
    expect(screen.getByText(/Connection blocked/)).toBeInTheDocument();
    expect(screen.getByText(STALE.fingerprint)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Keep blocked' })).toHaveFocus();
    expect(screen.queryByRole('button', { name: 'Accept once' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Accept and save' })).not.toBeInTheDocument();
  });

  it('requires a separate explicit confirmation before replacing a changed key', async () => {
    const onDecide = vi.fn();
    const user = userEvent.setup();
    const pending = makePending({ status: 'changed', knownKeys: [STALE] });
    render(<HostKeyDialog pending={pending} error={null} busy={false} onDecide={onDecide} />);
    const replace = screen.getByRole('button', { name: 'Replace trusted key' });
    const confirmation = screen.getByRole('textbox', { name: 'Type web-1.example to enable replacing' });

    expect(replace).toBeDisabled();
    await user.type(confirmation, 'web-1');
    expect(replace).toBeDisabled();
    await user.type(confirmation, '.example');
    await user.click(replace);

    expect(onDecide).toHaveBeenCalledWith('replace', true);
  });

  it('keeps a changed key blocked on Escape', async () => {
    const onDecide = vi.fn();
    const user = userEvent.setup();
    render(<HostKeyDialog pending={makePending({ status: 'changed', knownKeys: [STALE] })} error={null} busy={false} onDecide={onDecide} />);

    await user.keyboard('{Escape}');

    expect(onDecide).toHaveBeenCalledWith('reject', false);
  });

  it('shows the decision error', () => {
    render(<HostKeyDialog pending={makePending()} error="Decision timed out." busy={false} onDecide={vi.fn()} />);

    expect(screen.getByText('Decision timed out.')).toBeInTheDocument();
  });
});

class FakeSocket {
  static instances: FakeSocket[] = [];
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

function json(body: unknown, status = 200): Promise<Response> {
  return Promise.resolve(new Response(JSON.stringify(body), { status }));
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

describe('HostKeyPrompt', () => {
  it('shows nothing without pending decisions', async () => {
    fetchMock.mockImplementation(() => json([]));

    render(<HostKeyPrompt />);

    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('opens the dialog when a pending decision is pushed and posts the decision', async () => {
    fetchMock.mockImplementation((input: string, init?: RequestInit) => {
      if (input === '/api/session') return json({ csrfToken: 'csrf' });
      if (init?.method === 'POST') return Promise.resolve(new Response(null, { status: 204 }));
      return json([]);
    });
    const user = userEvent.setup();
    render(<HostKeyPrompt />);
    await waitFor(() => expect(fetchMock).toHaveBeenCalled());

    act(() => FakeSocket.instances[0]!.emit({ type: 'hostKeys.pendingChanged', data: [makePending()] }));
    await user.click(await screen.findByRole('button', { name: 'Accept and save' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    const post = fetchMock.mock.calls.find(([, init]) => (init as RequestInit | undefined)?.method === 'POST')!;
    expect(post[0]).toBe('/api/host-keys/pending/pending-1/decision');
    expect(JSON.parse((post[1] as RequestInit).body as string)).toEqual({ decision: 'acceptAndSave', confirmReplace: false });
  });

  it('closes the dialog when the decision disappears from the server list', async () => {
    fetchMock.mockImplementation(() => json([makePending()]));
    render(<HostKeyPrompt />);
    await screen.findByRole('dialog');

    act(() => FakeSocket.instances[0]!.emit({ type: 'hostKeys.pendingChanged', data: [] }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('shows the server error when the decision is refused', async () => {
    fetchMock.mockImplementation((input: string, init?: RequestInit) => {
      if (input === '/api/session') return json({ csrfToken: 'csrf' });
      if (init?.method === 'POST') return json({ detail: 'No pending host key decision.' }, 404);
      return json([makePending()]);
    });
    const user = userEvent.setup();
    render(<HostKeyPrompt />);

    await user.click(await screen.findByRole('button', { name: 'Reject' }));

    expect(await screen.findByText('No pending host key decision.')).toBeInTheDocument();
  });
});

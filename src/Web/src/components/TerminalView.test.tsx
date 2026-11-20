import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { TerminalSession } from '../api/terminals';
import { selectServer } from '../state/selectedServer';
import { dockPointer, measure } from '../test/DockingPointer';
import { renderInWorkspace } from '../test/FloatingTestWorkspace';
import { TerminalView } from './TerminalView';
import { TerminalWorkspace } from './TerminalWorkspace';

const mocks = vi.hoisted(() => {
  class FakeTerminal {
    static instances: FakeTerminal[] = [];
    cols = 80;
    rows = 24;
    written: Array<string | Uint8Array> = [];
    dataListener: ((data: string) => void) | null = null;
    disposed = false;

    constructor() {
      FakeTerminal.instances.push(this);
    }

    loadAddon(): void {}

    open(): void {}

    onData(listener: (data: string) => void) {
      this.dataListener = listener;
      return { dispose: () => undefined };
    }

    onBinary() {
      return { dispose: () => undefined };
    }

    write(data: string | Uint8Array): void {
      this.written.push(data);
    }

    dispose(): void {
      this.disposed = true;
    }
  }

  class FakeWebSocket {
    static readonly OPEN = 1;
    static instances: FakeWebSocket[] = [];
    readonly url: string;
    readyState = 0;
    binaryType = 'blob';
    sent: Array<string | Uint8Array> = [];
    closed = false;
    onopen: (() => void) | null = null;
    onmessage: ((event: { data: unknown }) => void) | null = null;
    onclose: ((event: { reason: string }) => void) | null = null;

    constructor(url: string) {
      this.url = url;
      FakeWebSocket.instances.push(this);
    }

    send(data: string | Uint8Array): void {
      this.sent.push(data);
    }

    close(): void {
      this.closed = true;
    }
  }

  return { FakeTerminal, FakeWebSocket, openTerminal: vi.fn(), closeTerminal: vi.fn() };
});

vi.mock('@xterm/xterm', () => ({ Terminal: mocks.FakeTerminal }));
vi.mock('@xterm/addon-fit', () => ({
  FitAddon: class {
    fit(): void {}
  },
}));
vi.mock('../api/terminals', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/terminals')>()),
  openTerminal: mocks.openTerminal,
  closeTerminal: mocks.closeTerminal,
}));

const SESSION: TerminalSession = {
  id: 'a1b2c3',
  connectionId: 'web-id',
  connectionName: 'web',
  sessionNumber: 1,
  openedAt: '2026-10-26T10:00:00Z',
};

beforeEach(() => {
  mocks.FakeTerminal.instances = [];
  mocks.FakeWebSocket.instances = [];
  mocks.openTerminal.mockReset();
  mocks.closeTerminal.mockReset();
  mocks.closeTerminal.mockResolvedValue(undefined);
  vi.stubGlobal('WebSocket', mocks.FakeWebSocket);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

async function renderConnected(onStateChange = vi.fn(), onOpened = vi.fn()) {
  mocks.openTerminal.mockResolvedValue(SESSION);
  const view = render(
    <TerminalView connectionId="web-id" active onOpened={onOpened} onStateChange={onStateChange} />,
  );
  await waitFor(() => expect(mocks.FakeWebSocket.instances).toHaveLength(1));
  const socket = mocks.FakeWebSocket.instances[0]!;
  act(() => {
    socket.readyState = mocks.FakeWebSocket.OPEN;
    socket.onopen?.();
  });
  return { view, socket, terminal: mocks.FakeTerminal.instances[0]! };
}

describe('TerminalView', () => {
  it('opens the session with the terminal size and attaches its WebSocket', async () => {
    const onOpened = vi.fn();
    const { socket } = await renderConnected(vi.fn(), onOpened);

    expect(mocks.openTerminal).toHaveBeenCalledWith('web-id', { columns: 80, rows: 24 });
    expect(onOpened).toHaveBeenCalledWith(SESSION);
    expect(socket.url).toMatch(/^ws:\/\/[^/]+\/api\/terminals\/a1b2c3\/ws$/);
    expect(socket.binaryType).toBe('arraybuffer');
    expect(socket.sent[0]).toBe('{"type":"resize","columns":80,"rows":24}');
  });

  it('relays keystrokes as binary frames and writes output to the terminal', async () => {
    const { socket, terminal } = await renderConnected();

    act(() => terminal.dataListener?.('ls\r'));
    act(() => socket.onmessage?.({ data: new Uint8Array([104, 105]).buffer }));

    const keystrokes = socket.sent[1] as Uint8Array;
    expect(new TextDecoder().decode(keystrokes)).toBe('ls\r');
    expect(Array.from(terminal.written[0] as Uint8Array)).toEqual([104, 105]);
  });

  it('reports connected and disconnected states with the reason', async () => {
    const onStateChange = vi.fn();
    const { socket } = await renderConnected(onStateChange);

    act(() => socket.onmessage?.({ data: '{"type":"status","state":"connected"}' }));
    act(() => socket.onmessage?.({ data: '{"type":"status","state":"disconnected","reason":"Remote shell exited"}' }));
    act(() => socket.onclose?.({ reason: 'terminal closed' }));

    expect(onStateChange.mock.calls).toEqual([
      ['connected', null],
      ['disconnected', 'Remote shell exited'],
    ]);
  });

  it('reports a lost connection when the socket closes unexpectedly', async () => {
    const onStateChange = vi.fn();
    const { socket } = await renderConnected(onStateChange);

    act(() => socket.onclose?.({ reason: '' }));

    expect(onStateChange).toHaveBeenCalledWith('disconnected', 'The connection to TermForge was lost.');
  });

  it('reports a failed open as disconnected', async () => {
    const onStateChange = vi.fn();
    mocks.openTerminal.mockRejectedValue(new Error('boom'));

    render(<TerminalView connectionId="web-id" active onOpened={vi.fn()} onStateChange={onStateChange} />);

    await waitFor(() => expect(onStateChange).toHaveBeenCalledWith('disconnected', 'Request failed.'));
    expect(mocks.FakeWebSocket.instances).toHaveLength(0);
  });

  it('closes the socket and disposes the terminal on unmount', async () => {
    const { view, socket, terminal } = await renderConnected();

    view.unmount();

    expect(socket.closed).toBe(true);
    expect(terminal.disposed).toBe(true);
  });

  it('closes a session that finished opening after unmount', async () => {
    let resolveOpen: (session: TerminalSession) => void = () => undefined;
    mocks.openTerminal.mockReturnValue(
      new Promise<TerminalSession>((resolve) => {
        resolveOpen = resolve;
      }),
    );
    const view = render(<TerminalView connectionId="web-id" active onOpened={vi.fn()} onStateChange={vi.fn()} />);

    view.unmount();
    await act(async () => resolveOpen(SESSION));

    expect(mocks.closeTerminal).toHaveBeenCalledWith('a1b2c3');
    expect(mocks.FakeWebSocket.instances).toHaveLength(0);
  });
});

async function renderFloatingTerminal() {
  selectServer(SESSION.connectionId);
  mocks.openTerminal.mockResolvedValue(SESSION);
  vi.stubGlobal('fetch', vi.fn(async () => new Response(null, { status: 401 })));
  const view = renderInWorkspace(<TerminalWorkspace />);
  fireEvent.click(screen.getByRole('button', { name: 'New session' }));
  await waitFor(() => expect(mocks.FakeWebSocket.instances).toHaveLength(1));
  const panel = screen.getByRole('tabpanel', { name: /web #1/ });
  const terminal = within(panel).getByTestId('terminal-view');
  const id = panel.id.slice('panel-'.length);
  view.detach(id);
  return { ...view, id, terminal, socket: mocks.FakeWebSocket.instances[0]! };
}

describe('floating terminal sessions', () => {
  afterEach(() => { selectServer(null); });

  it('keeps one terminal, WebSocket and PTY when its tab is dragged into a workspace split', async () => {
    const { id, terminal, container, socket } = await renderFloatingTerminal();
    const floating = screen.getByRole('region', { name: 'web #1' });
    fireEvent.click(within(floating).getByRole('button', { name: 'Dock' }));
    const tab = screen.getByRole('tab', { name: /web #1/ });
    measure(tab.closest('[role="tablist"]')!, { x: 280, y: 125, width: 240, height: 36 });
    const pointer = dockPointer(container);
    pointer.down(tab, 340, 145);
    pointer.move(530, 44);
    pointer.up(530, 44);

    expect(terminal.closest('.workspace-dock-split')).toBeInTheDocument();
    expect(screen.getByTestId('terminal-view')).toBe(terminal);
    expect(terminal.closest('[data-workspace-panel-content]')).toHaveAttribute('data-workspace-panel-content', id);
    expect(mocks.FakeTerminal.instances).toHaveLength(1);
    expect(mocks.FakeWebSocket.instances).toEqual([socket]);
    expect(socket.closed).toBe(false);
    expect(mocks.openTerminal).toHaveBeenCalledOnce();
    expect(mocks.closeTerminal).not.toHaveBeenCalled();
  });

  it('keeps the terminal, WebSocket and PTY across dock and detach, and confirms an allocated session before closing', async () => {
    HTMLDialogElement.prototype.showModal = function () { this.open = true; this.querySelector('button')?.focus(); };
    HTMLDialogElement.prototype.close = function () { this.open = false; };
    const { id, terminal, socket, detach } = await renderFloatingTerminal();
    let floating = screen.getByRole('region', { name: 'web #1' });
    expect(floating).toContainElement(terminal);
    fireEvent.click(within(floating).getByRole('button', { name: 'Dock' }));
    expect(screen.getByTestId('terminal-view')).toBe(terminal);
    detach(id);
    floating = screen.getByRole('region', { name: 'web #1' });
    expect(floating).toContainElement(terminal);
    expect(mocks.FakeTerminal.instances).toHaveLength(1);
    expect(mocks.FakeWebSocket.instances).toEqual([socket]);
    expect(mocks.openTerminal).toHaveBeenCalledOnce();
    expect(mocks.closeTerminal).not.toHaveBeenCalled();
    expect(socket.closed).toBe(false);
    expect(mocks.FakeTerminal.instances[0]?.disposed).toBe(false);

    const close = within(floating).getByRole('button', { name: 'Close floating window' });
    fireEvent.click(close);
    const cancel = within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Cancel' });
    expect(cancel).toHaveFocus();
    fireEvent.click(cancel);
    expect(floating).toContainElement(terminal);
    expect(socket.closed).toBe(false);
    expect(mocks.closeTerminal).not.toHaveBeenCalled();

    fireEvent.click(close);
    fireEvent.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Close session' }));
    expect(terminal).not.toBeInTheDocument();
    expect(socket.closed).toBe(true);
    expect(mocks.closeTerminal).toHaveBeenCalledExactlyOnceWith(SESSION.id);
  });

  it('closes a disconnected terminal without prompting', async () => {
    const { terminal, socket } = await renderFloatingTerminal();
    act(() => socket.onclose?.({ reason: 'Remote shell exited' }));
    fireEvent.click(screen.getByRole('button', { name: 'Close floating window' }));

    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(terminal).not.toBeInTheDocument();
    expect(mocks.closeTerminal).toHaveBeenCalledWith(SESSION.id);
  });
});

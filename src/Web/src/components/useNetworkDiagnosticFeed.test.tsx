import { act, render } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useNetworkDiagnosticFeed } from './useNetworkDiagnosticFeed';
import { refreshDiagnostic, receiveDiagnosticSnapshot } from '../state/networkDiagnostics';

const sockets = vi.hoisted(() => [] as Array<{ onopen: (() => void) | null; onclose: (() => void) | null; onmessage: ((event: { data: string }) => void) | null; close: ReturnType<typeof vi.fn> }>);
vi.mock('../api/webSocket', () => ({ openWebSocket: () => {
  const socket = { onopen: null, onclose: null, onmessage: null, close: vi.fn() };
  sockets.push(socket);
  return socket;
} }));
vi.mock('../state/networkDiagnostics', () => ({
  refreshDiagnostic: vi.fn().mockResolvedValue(undefined), receiveDiagnosticSnapshot: vi.fn(), getNetworkDiagnostics: () => ({ run: { status: 'running' } }),
}));

function Harness() { useNetworkDiagnosticFeed(); return null; }
afterEach(() => { vi.useRealTimers(); sockets.length = 0; vi.clearAllMocks(); });

describe('diagnostic realtime recovery', () => {
  it('refreshes on reconnection and cleans up its socket and timers', () => {
    vi.useFakeTimers();
    const view = render(<Harness />);
    act(() => sockets[0]!.onclose?.());
    act(() => { vi.advanceTimersByTime(2000); });
    expect(sockets).toHaveLength(2);
    act(() => sockets[1]!.onopen?.());
    expect(refreshDiagnostic).toHaveBeenCalledTimes(2);
    act(() => sockets[1]!.onmessage?.({ data: JSON.stringify({ type: 'network-diagnostics.changed', data: { id: 'run' } }) }));
    expect(receiveDiagnosticSnapshot).toHaveBeenCalledWith({ id: 'run' });
    view.unmount();
    expect(sockets[1]!.close).toHaveBeenCalled();
    act(() => { vi.advanceTimersByTime(10000); });
    expect(sockets).toHaveLength(2);
  });
});

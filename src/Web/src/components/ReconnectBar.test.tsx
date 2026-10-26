import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ReconnectMode, SavedConnection } from '../api/connections';
import { ReconnectBar } from './ReconnectBar';

const mocks = vi.hoisted(() => ({ getConnection: vi.fn() }));

vi.mock('../api/connections', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/connections')>()),
  getConnection: mocks.getConnection,
}));

function connectionWith(mode: ReconnectMode): SavedConnection {
  return { id: 'c1', reconnectMode: mode } as SavedConnection;
}

beforeEach(() => {
  mocks.getConnection.mockReset();
});

describe('ReconnectBar', () => {
  it('offers a manual reconnect button and never reconnects by itself in manual mode', async () => {
    mocks.getConnection.mockResolvedValue(connectionWith('Manual'));
    const onReconnect = vi.fn();
    render(<ReconnectBar connectionId="c1" reason="connection lost" wasConnected onReconnect={onReconnect} />);

    const button = await screen.findByRole('button', { name: 'Reconnect' });
    expect(onReconnect).not.toHaveBeenCalled();
    await userEvent.click(button);

    expect(onReconnect).toHaveBeenCalledTimes(1);
    expect(screen.getByText('Disconnected: connection lost')).toBeInTheDocument();
  });

  it('reconnects once by itself in automatic mode after a live session was lost', async () => {
    mocks.getConnection.mockResolvedValue(connectionWith('Automatic'));
    const onReconnect = vi.fn();
    const { rerender } = render(
      <ReconnectBar connectionId="c1" reason="lost" wasConnected onReconnect={onReconnect} />,
    );

    await waitFor(() => expect(onReconnect).toHaveBeenCalledTimes(1));
    rerender(<ReconnectBar connectionId="c1" reason="lost" wasConnected onReconnect={onReconnect} />);

    expect(onReconnect).toHaveBeenCalledTimes(1);
  });

  it('does not loop automatically when the very first connection failed', async () => {
    mocks.getConnection.mockResolvedValue(connectionWith('Automatic'));
    const onReconnect = vi.fn();
    render(<ReconnectBar connectionId="c1" reason="authentication failed" wasConnected={false} onReconnect={onReconnect} />);

    expect(await screen.findByRole('button', { name: 'Reconnect' })).toBeInTheDocument();
    expect(onReconnect).not.toHaveBeenCalled();
  });

  it('shows no reconnect action when the policy is off', async () => {
    mocks.getConnection.mockResolvedValue(connectionWith('Off'));
    const onReconnect = vi.fn();
    render(<ReconnectBar connectionId="c1" reason="lost" wasConnected onReconnect={onReconnect} />);

    await waitFor(() => expect(mocks.getConnection).toHaveBeenCalled());
    expect(screen.queryByRole('button', { name: 'Reconnect' })).not.toBeInTheDocument();
    expect(onReconnect).not.toHaveBeenCalled();
  });

  it('shows no reconnect action when the policy cannot be loaded', async () => {
    mocks.getConnection.mockRejectedValue(new Error('offline'));
    render(<ReconnectBar connectionId="c1" reason="lost" wasConnected onReconnect={vi.fn()} />);

    await waitFor(() => expect(mocks.getConnection).toHaveBeenCalled());
    expect(screen.queryByRole('button', { name: 'Reconnect' })).not.toBeInTheDocument();
  });
});

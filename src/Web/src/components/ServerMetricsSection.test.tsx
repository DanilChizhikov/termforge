import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import * as api from '../api/monitoring';
import type { ServerMetrics } from '../api/monitoring';
import { setMonitoringInterval } from '../state/monitoringInterval';
import { ServerMetricsSection } from './ServerMetricsSection';

vi.mock('../api/monitoring', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api/monitoring')>();
  return { ...actual, getServerMetrics: vi.fn() };
});

const GIB = 1024 ** 3;
const getMetrics = vi.mocked(api.getServerMetrics);

function makeMetrics(overrides: Partial<ServerMetrics> = {}): ServerMetrics {
  return {
    connectionId: 'srv-1',
    state: 'Connected',
    collectedAt: '2026-10-26T10:00:00Z',
    message: null,
    uptimeSeconds: { available: true, value: 12 * 86400 + 4 * 3600, reason: null },
    cpuPercent: { available: true, value: 23.4, reason: null },
    memory: { available: true, value: { usedBytes: 2 * GIB, totalBytes: 4 * GIB }, reason: null },
    disk: { available: true, value: { freeBytes: 25 * GIB, totalBytes: 80 * GIB, mountPoint: '/' }, reason: null },
    ...overrides,
  };
}

beforeEach(() => {
  vi.useFakeTimers();
  act(() => setMonitoringInterval(30));
});

afterEach(() => {
  vi.useRealTimers();
  vi.clearAllMocks();
});

async function flush() {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(0);
  });
}

async function advance(milliseconds: number) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(milliseconds);
  });
}

describe('ServerMetricsSection', () => {
  it('shows measured values', async () => {
    getMetrics.mockResolvedValue(makeMetrics());

    render(<ServerMetricsSection serverId="srv-1" />);
    await flush();

    expect(within(screen.getByRole('group', { name: 'Uptime' })).getByText('12 d 4 h')).toBeInTheDocument();
    expect(within(screen.getByRole('group', { name: 'CPU' })).getByText('23%')).toBeInTheDocument();
    expect(screen.getByRole('group', { name: 'Memory' })).toHaveTextContent('of 4.0 GB');
    expect(screen.getByRole('group', { name: 'Disk' })).toHaveTextContent('25.0 GB');
    expect(screen.getByRole('progressbar', { name: 'Disk used' })).toHaveValue(69);
  });

  it('shows the reason instead of a value for a metric that cannot be collected', async () => {
    getMetrics.mockResolvedValue(
      makeMetrics({ cpuPercent: { available: false, value: null, reason: 'top did not report CPU usage.' } }),
    );

    render(<ServerMetricsSection serverId="srv-1" />);
    await flush();

    const card = screen.getByRole('group', { name: 'CPU' });
    expect(card).toHaveTextContent('Not measured');
    expect(card).toHaveTextContent('top did not report CPU usage.');
    expect(screen.queryByRole('progressbar', { name: 'CPU usage' })).not.toBeInTheDocument();
  });

  it('polls on the configured interval and stops when the view is closed', async () => {
    getMetrics.mockResolvedValue(makeMetrics());

    const view = render(<ServerMetricsSection serverId="srv-1" />);
    await flush();
    expect(getMetrics).toHaveBeenCalledTimes(1);

    await advance(30_000);
    expect(getMetrics).toHaveBeenCalledTimes(2);

    view.unmount();
    await advance(120_000);
    expect(getMetrics).toHaveBeenCalledTimes(2);
  });

  it('uses a changed interval for the next poll', async () => {
    getMetrics.mockResolvedValue(makeMetrics());
    const view = render(<ServerMetricsSection serverId="srv-1" />);
    await flush();

    act(() => setMonitoringInterval(10));
    await advance(10_000);

    expect(getMetrics).toHaveBeenCalledTimes(2);
    view.unmount();
  });

  it('stops polling when the server disconnects and resumes on request', async () => {
    getMetrics.mockResolvedValueOnce(makeMetrics({ state: 'ConnectionFailed', message: 'connection reset' }));

    const view = render(<ServerMetricsSection serverId="srv-1" />);
    await flush();
    await advance(120_000);

    expect(getMetrics).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('group', { name: 'Connection' })).toHaveTextContent('connection reset');
    expect(screen.getByText(/polling stopped/)).toBeInTheDocument();

    getMetrics.mockResolvedValue(makeMetrics());
    fireEvent.click(screen.getByRole('button', { name: 'Measure now' }));
    await flush();

    expect(getMetrics).toHaveBeenCalledTimes(2);
    expect(screen.getByRole('group', { name: 'Connection' })).toHaveTextContent('Connected');
    view.unmount();
  });

  it('stops polling when a request fails', async () => {
    getMetrics.mockRejectedValue(new Error('boom'));

    const view = render(<ServerMetricsSection serverId="srv-1" />);
    await flush();
    await advance(120_000);

    expect(getMetrics).toHaveBeenCalledTimes(1);
    expect(screen.getByText('Could not load server metrics.')).toBeInTheDocument();
    view.unmount();
  });
});

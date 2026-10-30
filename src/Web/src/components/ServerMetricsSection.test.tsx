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
    latencyMilliseconds: 38,
    uptimeSeconds: { available: true, value: 12 * 86400 + 4 * 3600, reason: null },
    cpuPercent: { available: true, value: 23.4, reason: null },
    memory: { available: true, value: { usedBytes: 2 * GIB, totalBytes: 4 * GIB }, reason: null },
    disk: { available: true, value: { freeBytes: 25 * GIB, totalBytes: 80 * GIB, mountPoint: '/' }, reason: null },
    loadAverage: { available: true, value: { oneMinute: 0.42, fiveMinutes: 0.38, fifteenMinutes: 0.35 }, reason: null },
    cpuCount: { available: true, value: 2, reason: null },
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
    expect(screen.getByRole('group', { name: 'Disk' })).toHaveTextContent('25 GB free of 80');
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
    expect(screen.queryByRole('img', { name: /CPU usage/ })).not.toBeInTheDocument();
  });

  it('shows latency, connected time, load average and CPU count', async () => {
    getMetrics.mockResolvedValue(makeMetrics());

    const view = render(<ServerMetricsSection serverId="srv-1" />);
    await flush();

    const connection = screen.getByRole('group', { name: 'Connection' });
    expect(connection).toHaveTextContent('38 ms');
    expect(connection).toHaveTextContent('Connected 0 m · 0 drops today');
    expect(screen.getByRole('group', { name: 'Uptime' })).toHaveTextContent('Load 0.42 · 0.38 · 0.35');
    expect(screen.getByRole('group', { name: 'CPU' })).toHaveTextContent('2 vCPU');
    view.unmount();
  });

  it('hides load average and CPU count when they are not measured', async () => {
    const unavailable = { available: false, value: null, reason: 'not reported' };
    getMetrics.mockResolvedValue(makeMetrics({ loadAverage: unavailable, cpuCount: unavailable, latencyMilliseconds: null }));

    const view = render(<ServerMetricsSection serverId="srv-1" />);
    await flush();

    expect(screen.getByRole('group', { name: 'Connection' })).toHaveTextContent(/^ConnectionConnected/);
    expect(screen.getByRole('group', { name: 'Uptime' })).not.toHaveTextContent('Load');
    expect(screen.getByRole('group', { name: 'CPU' })).not.toHaveTextContent('vCPU');
    view.unmount();
  });

  it('draws the CPU history of the polls as a chart', async () => {
    getMetrics.mockResolvedValueOnce(makeMetrics({ collectedAt: '2026-10-26T10:00:00Z' }));
    getMetrics.mockResolvedValueOnce(
      makeMetrics({ collectedAt: '2026-10-26T10:05:00Z', cpuPercent: { available: true, value: 41, reason: null } }),
    );

    const view = render(<ServerMetricsSection serverId="srv-1" />);
    await flush();
    expect(screen.getByRole('img', { name: 'CPU usage over the last minute, 23 percent' })).toBeInTheDocument();

    await advance(30_000);

    expect(
      screen.getByRole('img', { name: 'CPU usage over the last 5 minutes, between 23 and 41 percent' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('progressbar', { name: 'CPU usage' })).not.toBeInTheDocument();
    view.unmount();
  });

  it('counts a lost connection as a drop and measures connected time from the reconnect', async () => {
    getMetrics.mockResolvedValueOnce(makeMetrics({ collectedAt: '2026-10-26T10:00:00Z' }));
    getMetrics.mockResolvedValueOnce(makeMetrics({ state: 'ConnectionFailed', message: 'connection reset' }));

    const view = render(<ServerMetricsSection serverId="srv-1" />);
    await flush();
    await advance(30_000);

    getMetrics.mockResolvedValueOnce(makeMetrics({ collectedAt: '2026-10-26T11:00:00Z' }));
    getMetrics.mockResolvedValueOnce(makeMetrics({ collectedAt: '2026-10-26T12:14:00Z' }));
    fireEvent.click(screen.getByRole('button', { name: 'Measure now' }));
    await flush();
    await advance(30_000);

    expect(screen.getByRole('group', { name: 'Connection' })).toHaveTextContent('Connected 1 h 14 m · 1 drop today');
    view.unmount();
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

  it('shows skeleton cards until the first metrics arrive', async () => {
    let resolveMetrics: (metrics: ServerMetrics) => void = () => {};
    const pendingMetrics = new Promise<ServerMetrics>((resolve) => {
      resolveMetrics = resolve;
    });
    getMetrics.mockReturnValue(pendingMetrics);

    const view = render(<ServerMetricsSection serverId="srv-skeleton" />);
    await flush();

    expect(screen.getByText('Collecting metrics…')).toBeInTheDocument();
    expect(screen.getByTestId('metric-cards-skeleton')).toHaveAttribute('aria-busy', 'true');

    const metrics = makeMetrics({ connectionId: 'srv-skeleton' });
    resolveMetrics(metrics);
    await flush();

    expect(screen.queryByTestId('metric-cards-skeleton')).not.toBeInTheDocument();
    expect(screen.getByRole('group', { name: 'Uptime' })).toBeInTheDocument();
    view.unmount();
  });
});

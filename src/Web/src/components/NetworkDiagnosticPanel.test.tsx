import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as api from '../api/networkDiagnostics';
import type { SavedConnection } from '../api/connections';
import { replaceConnections } from '../state/connections';
import { getNetworkDiagnostics, openNetworkDiagnostics, resetNetworkDiagnostics, updateDiagnosticDraft } from '../state/networkDiagnostics';
import { NetworkDiagnosticPanel } from './NetworkDiagnosticPanel';

vi.mock('./useNetworkDiagnosticFeed', () => ({ useNetworkDiagnosticFeed: () => undefined }));
vi.mock('../api/networkDiagnostics', async (original) => ({
  ...await original<typeof import('../api/networkDiagnostics')>(),
  prepareDiagnostic: vi.fn(), startDiagnostic: vi.fn(), getDiagnostic: vi.fn(), getDiagnosticResults: vi.fn(), cancelDiagnostic: vi.fn(),
  prepareRemoteAction: vi.fn(), executeRemoteAction: vi.fn(),
}));

const snapshot: api.DiagnosticSnapshot = { id: 'run-1', host: 'example.com', status: 'completed', completed: 5, total: 5, points: [{ connectionId: null, category: 'rf' }], error: null };
const preparation: api.DiagnosticPreparation = { plan: { host: 'example.com', ports: [22], fullScan: false, probesPerPoint: 1, points: snapshot.points }, confirmation: null };

beforeEach(() => {
  vi.resetAllMocks();
  resetNetworkDiagnostics();
  vi.mocked(api.prepareDiagnostic).mockResolvedValue(preparation);
  vi.mocked(api.startDiagnostic).mockResolvedValue(snapshot);
  vi.mocked(api.getDiagnostic).mockResolvedValue(snapshot);
  vi.mocked(api.getDiagnosticResults).mockResolvedValue([]);
  vi.mocked(api.cancelDiagnostic).mockResolvedValue(undefined);
  replaceConnections([{ id: 'server-a', name: 'Server A', host: 'example.com', readOnly: false } as SavedConnection]);
});

describe('network diagnostics', () => {
  it('runs only on request and keeps its form and results across navigation', async () => {
    const user = userEvent.setup();
    const view = render(<NetworkDiagnosticPanel />);
    expect(api.prepareDiagnostic).not.toHaveBeenCalled();
    await user.type(screen.getByLabelText('Host or IP'), 'example.com');
    await user.click(screen.getByRole('button', { name: 'Run network checks' }));
    await screen.findByRole('region', { name: 'Network check results' });
    view.unmount();
    render(<NetworkDiagnosticPanel />);
    expect(screen.getByLabelText('Host or IP')).toHaveValue('example.com');
    expect(screen.getByRole('region', { name: 'Network check results' })).toHaveTextContent('completed');
    expect(api.startDiagnostic).toHaveBeenCalledTimes(1);
  });

  it('requires explicit full scan confirmation and sends the exact prepared request', async () => {
    const user = userEvent.setup();
    vi.mocked(api.prepareDiagnostic).mockResolvedValue({ ...preparation, confirmation: { token: 'grant', expiresAt: '2099-01-01' } });
    render(<NetworkDiagnosticPanel />);
    await user.type(screen.getByLabelText('Host or IP'), 'example.com');
    await user.click(screen.getByLabelText(/All 65,535/));
    await user.click(screen.getByRole('button', { name: 'Run network checks' }));
    await screen.findByRole('alertdialog', { name: 'Confirm full port scan' });
    expect(api.startDiagnostic).not.toHaveBeenCalled();
    await user.click(screen.getByRole('button', { name: 'Confirm full scan' }));
    await waitFor(() => expect(api.startDiagnostic).toHaveBeenCalledWith(expect.objectContaining({ host: 'example.com', allPorts: true, confirmationToken: 'grant' })));
  });

  it('invalidates a prepared scan when its form changes', async () => {
    const user = userEvent.setup();
    vi.mocked(api.prepareDiagnostic).mockResolvedValue({ ...preparation, confirmation: { token: 'grant', expiresAt: '2099-01-01' } });
    render(<NetworkDiagnosticPanel />);
    await user.click(screen.getByRole('button', { name: 'Run network checks' }));
    await screen.findByRole('alertdialog');
    await user.type(screen.getByLabelText('Host or IP'), 'changed.example');
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(api.startDiagnostic).not.toHaveBeenCalled();
  });

  it('shows distinct outcomes even when a point or tool is unavailable', async () => {
    const statuses: api.ProbeOutcome['status'][] = ['responded', 'refused', 'noResponse', 'error', 'unavailable'];
    vi.mocked(api.getDiagnosticResults).mockResolvedValue(statuses.map((status, sequence) => ({
      sequence, point: { connectionId: null, category: 'rf' }, pointName: 'Local machine', protocol: 'udp', port: 53,
      outcome: { status, detail: `detail-${status}` }, elapsedMilliseconds: 12,
    })));
    render(<NetworkDiagnosticPanel />);
    fireEvent.click(screen.getByRole('button', { name: 'Run network checks' }));
    expect(await screen.findByText('Responded')).toBeInTheDocument();
    const labels = ['Refused', 'No response', 'Error', 'Unavailable'];
    for (let i = 0; i < labels.length; i++) expect(screen.getByText(labels[i]!)).toBeInTheDocument();
    expect(screen.getByText(/UDP silence/)).toBeInTheDocument();
  });

  it('cancels an active run and retains collected results', async () => {
    vi.mocked(api.startDiagnostic).mockResolvedValue({ ...snapshot, status: 'running' });
    vi.mocked(api.getDiagnostic).mockResolvedValue({ ...snapshot, status: 'running' });
    render(<NetworkDiagnosticPanel />);
    fireEvent.click(screen.getByRole('button', { name: 'Run network checks' }));
    const cancel = await screen.findByRole('button', { name: 'Cancel checks' });
    await waitFor(() => expect(cancel).toBeEnabled());
    vi.mocked(api.getDiagnostic).mockResolvedValue({ ...snapshot, status: 'cancelled' });
    fireEvent.click(cancel);
    await waitFor(() => expect(api.cancelDiagnostic).toHaveBeenCalledWith('run-1'));
    await waitFor(() => expect(screen.getByRole('region', { name: 'Network check results' })).toHaveTextContent('cancelled'));
  });

  it('reports request errors without losing the form', async () => {
    vi.mocked(api.prepareDiagnostic).mockRejectedValue(new Error('unavailable'));
    render(<NetworkDiagnosticPanel />);
    fireEvent.change(screen.getByLabelText('Host or IP'), { target: { value: 'example.com' } });
    fireEvent.click(screen.getByRole('button', { name: 'Run network checks' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Request failed');
    expect(screen.getByLabelText('Host or IP')).toHaveValue('example.com');
  });

  it('prefills the saved target from a connection error without starting checks', () => {
    openNetworkDiagnostics('server-a');
    render(<NetworkDiagnosticPanel />);
    expect(screen.getByLabelText('Target')).toHaveValue('server-a');
    expect(api.startDiagnostic).not.toHaveBeenCalled();
  });

  it('reviews remote actions before execution and clears the sudo password', async () => {
    updateDiagnosticDraft({ points: [{ connectionId: 'server-a', category: 'outsideRf' }], host: 'example.com' });
    const plan: api.RemoteActionPlan = { id: 'action', connectionId: 'server-a', serverName: 'Server A', kind: 'install', packages: ['curl'], command: 'sudo install curl', requiresSudo: true, confirmationToken: 'confirm', expiresAt: '2099-01-01', warning: 'Packages already changed cannot be rolled back.' };
    vi.mocked(api.prepareRemoteAction).mockResolvedValue(plan);
    vi.mocked(api.executeRemoteAction).mockResolvedValue({ outcome: { status: 'responded', detail: 'Installed' }, capabilities: 'curl' });
    const user = userEvent.setup();
    render(<NetworkDiagnosticPanel />);
    await user.selectOptions(screen.getByLabelText('SSH point'), 'server-a');
    await user.click(screen.getByLabelText(/Use a sudo password/));
    await user.click(screen.getByRole('button', { name: 'Review remote action' }));
    await screen.findByRole('alertdialog', { name: 'Confirm remote diagnostic action' });
    expect(api.executeRemoteAction).not.toHaveBeenCalled();
    await user.type(screen.getByLabelText('Sudo password'), 'secret');
    await user.click(screen.getByRole('button', { name: 'Confirm remote action' }));
    await waitFor(() => expect(api.executeRemoteAction).toHaveBeenCalledWith(plan, 'secret'));
    await waitFor(() => expect(screen.queryByLabelText('Sudo password')).not.toBeInTheDocument());
    expect(JSON.stringify(getNetworkDiagnostics())).not.toContain('secret');
  });

  it('ignores stale result responses after changing pages', async () => {
    let resolve: (value: api.DiagnosticResult[]) => void = () => undefined;
    vi.mocked(api.getDiagnosticResults).mockImplementationOnce(() => new Promise((done) => { resolve = done; }));
    render(<NetworkDiagnosticPanel />);
    fireEvent.click(screen.getByRole('button', { name: 'Run network checks' }));
    await screen.findByRole('region', { name: 'Network check results' });
    act(() => resetNetworkDiagnostics());
    await act(async () => { resolve([]); });
    expect(getNetworkDiagnostics().run).toBeNull();
  });
});

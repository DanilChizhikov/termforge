import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, it, vi } from 'vitest';
import * as api from '../api/networkDiagnostics';
import { resetNetworkDiagnostics, runNetworkDiagnostic } from '../state/networkDiagnostics';
import { NetworkDiagnosticResults } from './NetworkDiagnosticResults';

vi.mock('../api/networkDiagnostics', async (original) => ({
  ...await original<typeof import('../api/networkDiagnostics')>(),
  prepareDiagnostic: vi.fn(), startDiagnostic: vi.fn(), getDiagnostic: vi.fn(), getDiagnosticResults: vi.fn(),
}));

const snapshot: api.DiagnosticSnapshot = { id: 'run', host: 'example.com', status: 'completed', completed: 200, total: 200, points: [{ connectionId: null, category: 'rf' }], error: null };
const detail = `CONNECTED\nCertificate chain\n${'s:CN = example.com\n'.repeat(40)}<script>alert('text')</script>`;

beforeEach(async () => {
  vi.resetAllMocks();
  resetNetworkDiagnostics();
  vi.mocked(api.prepareDiagnostic).mockResolvedValue({ plan: { host: 'example.com', ports: [443], fullScan: false, probesPerPoint: 1, points: snapshot.points }, confirmation: null });
  vi.mocked(api.startDiagnostic).mockResolvedValue(snapshot);
  vi.mocked(api.getDiagnostic).mockResolvedValue(snapshot);
  vi.mocked(api.getDiagnosticResults).mockResolvedValue(Array.from({ length: 100 }, (_, sequence) => ({
    sequence, point: { connectionId: null, category: 'outsideRf' }, pointName: 'Local machine', protocol: 'tls', port: 443,
    outcome: { status: 'responded', detail }, elapsedMilliseconds: 123,
  })));
  await runNetworkDiagnostic();
});

it('opens the received multiline details as text and restores keyboard focus after closing', async () => {
  const user = userEvent.setup();
  render(<NetworkDiagnosticResults />);
  expect(screen.getAllByRole('row')).toHaveLength(101);
  expect(screen.getAllByText('Outside Russia')).toHaveLength(100);
  const trigger = screen.getAllByRole('button', { name: 'View details for Local machine TLS port 443' })[0]!;
  await user.click(trigger);
  const dialog = screen.getByRole('dialog', { name: 'Check details' });
  expect(dialog.querySelector('pre')?.textContent).toBe(detail);
  expect(dialog.querySelector('script')).toBeNull();
  await user.keyboard('{Escape}');
  expect(trigger).toHaveFocus();
  await user.click(trigger);
  await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Close' }));
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
});

it('retains pagination while the displayed page scrolls independently', async () => {
  const user = userEvent.setup();
  render(<NetworkDiagnosticResults />);
  expect(screen.getByRole('button', { name: 'Previous results' })).toBeDisabled();
  await user.click(screen.getByRole('button', { name: 'Next results' }));
  expect(api.getDiagnosticResults).toHaveBeenLastCalledWith('run', 100);
  expect(await screen.findByText('101–200 of 200')).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Next results' })).toBeDisabled();
});

import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cancelInstallation, getInstallationStatus, installPreparedUpdate, prepareInstallation, type InstallationStatus } from '../../api/installation';
import { getPendingAttention } from '../../api/lifecycle';
import { flushWorkspaceStateStrict } from '../../state/workspacePersistence';
import { UpdateInstallDialog } from './UpdateInstallDialog';

vi.mock('../../api/installation', () => ({ getInstallationStatus: vi.fn(), prepareInstallation: vi.fn(), cancelInstallation: vi.fn(), installPreparedUpdate: vi.fn() }));
vi.mock('../../api/lifecycle', () => ({ getPendingAttention: vi.fn() }));
vi.mock('../../state/workspacePersistence', () => ({ flushWorkspaceStateStrict: vi.fn() }));

const prepared: InstallationStatus = { state: 'prepared', version: '1.3.0', supported: true, error: null, unavailableReason: null, previousVersion: '1.1.0', readyToRestart: false, operation: 'update' };

beforeEach(() => {
  vi.clearAllMocks();
  HTMLDialogElement.prototype.showModal = vi.fn();
  HTMLDialogElement.prototype.close = vi.fn();
  vi.mocked(getInstallationStatus).mockResolvedValue(prepared);
  vi.mocked(getPendingAttention).mockResolvedValue({ state: 'running', transfers: [], terminalSessions: [], unsavedFiles: [], idleShutdownMinutes: 0 });
  vi.mocked(flushWorkspaceStateStrict).mockResolvedValue(undefined);
  vi.mocked(installPreparedUpdate).mockResolvedValue({ ...prepared, state: 'restarting' });
});
afterEach(() => { vi.useRealTimers(); });

async function show(rollback = false) {
  if (rollback) vi.mocked(getInstallationStatus).mockResolvedValue({ ...prepared, operation: 'rollback' });
  const user = userEvent.setup();
  const onClose = vi.fn();
  render(<UpdateInstallDialog rollback={rollback} onClose={onClose} />);
  await act(async () => { await new Promise((resolve) => setTimeout(resolve, 1100)); });
  return { user, onClose };
}

describe('update installation', () => {
  it('requires confirmation and acknowledged draft persistence before restarting', async () => {
    const { user } = await show();
    expect(installPreparedUpdate).not.toHaveBeenCalled();
    await user.click(screen.getByRole('button', { name: 'Update and restart', hidden: true }));
    expect(flushWorkspaceStateStrict).toHaveBeenCalled();
    expect(installPreparedUpdate).toHaveBeenCalledWith(true, true);
  });

  it('blocks restart on a failed draft write', async () => {
    const { user } = await show();
    vi.mocked(flushWorkspaceStateStrict).mockRejectedValue(new Error('Draft write failed'));
    await user.click(screen.getByRole('button', { name: 'Update and restart', hidden: true }));
    expect(installPreparedUpdate).not.toHaveBeenCalled();
    expect(screen.getByRole('alert', { hidden: true })).toHaveTextContent('Draft write failed');
  });

  it('warns about lost changes and keeps the current-data copy enabled by default', async () => {
    const { user } = await show(true);
    expect(screen.getByText(/Changes made after that update will be lost/)).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { hidden: true })).toBeChecked();
    await user.click(screen.getByRole('button', { name: 'Roll back and restart', hidden: true }));
    expect(installPreparedUpdate).toHaveBeenCalledWith(true, true);
  });

  it('cancels a prepared operation', async () => {
    const { user, onClose } = await show();
    await user.click(screen.getByRole('button', { name: 'Cancel', hidden: true }));
    expect(cancelInstallation).toHaveBeenCalledOnce();
    expect(onClose).toHaveBeenCalledOnce();
    expect(prepareInstallation).not.toHaveBeenCalled();
  });

  it('keeps polling when a transfer starts immediately before installation', async () => {
    const { user } = await show();
    vi.mocked(installPreparedUpdate).mockResolvedValue({ ...prepared, state: 'waitingForTransfers' });
    await user.click(screen.getByRole('button', { name: 'Update and restart', hidden: true }));
    expect(screen.getByText(/Waiting for transfers/)).toBeInTheDocument();
    expect(screen.queryByText(/TermForge will open a new private launch link/)).not.toBeInTheDocument();
  });

  it('does not install an update through a rollback confirmation', async () => {
    vi.mocked(getInstallationStatus).mockResolvedValue({ ...prepared, operation: 'rollback' });
    await show();
    expect(screen.getByRole('button', { name: 'Update and restart', hidden: true })).toBeDisabled();
    expect(screen.getByText(/Another rollback operation is already prepared/)).toBeInTheDocument();
    expect(installPreparedUpdate).not.toHaveBeenCalled();
  });

  it('offers a manual retry when saving a draft fails after transfers finish', async () => {
    vi.mocked(getInstallationStatus).mockResolvedValue({ ...prepared, state: 'waitingForTransfers', readyToRestart: true });
    vi.mocked(flushWorkspaceStateStrict).mockRejectedValue(new Error('Draft write failed'));
    const { user } = await show();
    expect(installPreparedUpdate).not.toHaveBeenCalled();
    vi.mocked(flushWorkspaceStateStrict).mockResolvedValue(undefined);

    await user.click(screen.getByRole('button', { name: 'Retry restart', hidden: true }));

    expect(installPreparedUpdate).toHaveBeenCalledWith(false, true);
  });
});

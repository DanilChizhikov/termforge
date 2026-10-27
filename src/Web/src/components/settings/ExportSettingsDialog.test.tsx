import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/client';
import type { SettingsExport, SettingsExportRequest, SettingsTransferSummary } from '../../api/settingsTransfer';
import { ExportSettingsDialog } from './ExportSettingsDialog';

const mocks = vi.hoisted(() => ({
  getTransferSummary: vi.fn<() => Promise<SettingsTransferSummary>>(),
  exportSettings: vi.fn<(request: SettingsExportRequest) => Promise<SettingsExport>>(),
  flushWorkspaceState: vi.fn<() => Promise<void>>(),
}));

vi.mock('../../api/settingsTransfer', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/settingsTransfer')>()),
  getTransferSummary: mocks.getTransferSummary,
  exportSettings: mocks.exportSettings,
}));

vi.mock('../../state/workspacePersistence', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../state/workspacePersistence')>()),
  flushWorkspaceState: mocks.flushWorkspaceState,
}));

const SETTINGS_FILE = { fileName: 'termforge-settings-2026-10-27.json', content: '{"format":"termforge-settings"}' };

beforeEach(() => {
  mocks.getTransferSummary.mockReset().mockResolvedValue({ connections: 6, groups: 2, pinnedCommands: 14 });
  mocks.exportSettings.mockReset().mockResolvedValue({ settings: SETTINGS_FILE, secrets: null });
  mocks.flushWorkspaceState.mockReset().mockResolvedValue(undefined);
});

function renderDialog() {
  const saveFile = vi.fn();
  const onExported = vi.fn();
  const onClose = vi.fn();
  render(<ExportSettingsDialog onClose={onClose} onExported={onExported} saveFile={saveFile} />);
  return { saveFile, onExported, onClose };
}

describe('ExportSettingsDialog', () => {
  it('shows what each section contains and keeps secrets and pinned commands off by default', async () => {
    renderDialog();

    expect(await screen.findByText('6 servers · 2 groups')).toBeInTheDocument();
    expect(screen.getByText('14 commands')).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: /Server connections/ })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: /Pinned commands/ })).not.toBeChecked();
    expect(screen.getByRole('switch', { name: 'Also export secrets' })).toHaveAttribute('aria-checked', 'false');
    expect(screen.queryByLabelText('Archive password')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Export 1 file' })).toBeEnabled();
  });

  it('exports the settings file without secrets', async () => {
    const user = userEvent.setup();
    const { saveFile, onExported } = renderDialog();

    await user.click(screen.getByRole('button', { name: 'Export 1 file' }));

    expect(mocks.flushWorkspaceState).toHaveBeenCalled();
    expect(mocks.exportSettings).toHaveBeenCalledWith({
      sections: { connections: true, preferences: true, workspace: true, pinnedCommands: false },
      secrets: null,
    });
    expect(saveFile).toHaveBeenCalledWith(SETTINGS_FILE.fileName, SETTINGS_FILE.content, 'application/json');
    expect(onExported).toHaveBeenCalledWith([SETTINGS_FILE.fileName]);
  });

  it('requires a strong, repeated password and the confirmation before exporting secrets', async () => {
    const user = userEvent.setup();
    mocks.exportSettings.mockResolvedValue({
      settings: SETTINGS_FILE,
      secrets: { fileName: 'termforge-secrets-2026-10-27.tfvault', contentBase64: 'VEZWVAAB', secretCount: 2 },
    });
    const { saveFile, onExported } = renderDialog();

    await user.click(screen.getByRole('switch', { name: 'Also export secrets' }));
    const exportButton = screen.getByRole('button', { name: 'Export 2 files' });
    expect(exportButton).toBeDisabled();
    expect(screen.getByText('termforge-secrets-', { exact: false })).toBeInTheDocument();

    await user.type(screen.getByLabelText('Archive password'), 'short');
    expect(screen.getByTestId('password-strength')).toHaveTextContent('Too short');
    await user.clear(screen.getByLabelText('Archive password'));
    await user.type(screen.getByLabelText('Archive password'), 'correct-horse-staple');
    await user.type(screen.getByLabelText('Repeat password'), 'correct-horse-staple');
    expect(screen.getByTestId('password-strength')).toHaveTextContent('Strong · passwords match');
    expect(exportButton).toBeDisabled();

    await user.click(screen.getByRole('checkbox', { name: /anyone with this archive/ }));
    expect(exportButton).toBeEnabled();
    await user.click(exportButton);

    expect(mocks.exportSettings).toHaveBeenCalledWith(
      expect.objectContaining({ secrets: { password: 'correct-horse-staple', confirmed: true } }),
    );
    expect(saveFile).toHaveBeenCalledTimes(2);
    const [archiveName, archiveBytes, archiveType] = saveFile.mock.calls[1] as [string, Uint8Array, string];
    expect(archiveName).toBe('termforge-secrets-2026-10-27.tfvault');
    expect(Array.from(archiveBytes.slice(0, 4))).toEqual([0x54, 0x46, 0x56, 0x54]);
    expect(archiveType).toBe('application/octet-stream');
    expect(onExported).toHaveBeenCalledWith(['termforge-settings-2026-10-27.json', 'termforge-secrets-2026-10-27.tfvault']);
  });

  it('keeps the export disabled while the passwords differ', async () => {
    const user = userEvent.setup();
    renderDialog();

    await user.click(screen.getByRole('switch', { name: 'Also export secrets' }));
    await user.type(screen.getByLabelText('Archive password'), 'correct-horse-staple');
    await user.type(screen.getByLabelText('Repeat password'), 'correct-horse-stable');
    await user.click(screen.getByRole('checkbox', { name: /anyone with this archive/ }));

    expect(screen.getByTestId('password-strength')).toHaveTextContent('passwords differ');
    expect(screen.getByRole('button', { name: 'Export 2 files' })).toBeDisabled();
  });

  it('turns secrets off when server connections are excluded', async () => {
    const user = userEvent.setup();
    renderDialog();

    await user.click(screen.getByRole('switch', { name: 'Also export secrets' }));
    await user.click(screen.getByRole('checkbox', { name: /Server connections/ }));

    const secretsSwitch = screen.getByRole('switch', { name: 'Also export secrets' });
    expect(secretsSwitch).toHaveAttribute('aria-checked', 'false');
    expect(secretsSwitch).toBeDisabled();
  });

  it('shows the error returned by the backend', async () => {
    const user = userEvent.setup();
    mocks.exportSettings.mockRejectedValue(new ApiError(409, { detail: 'Saved passwords could not be read: the vault is locked.' }));
    const { onExported } = renderDialog();

    await user.click(screen.getByRole('button', { name: 'Export 1 file' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('the vault is locked');
    expect(onExported).not.toHaveBeenCalled();
  });
});

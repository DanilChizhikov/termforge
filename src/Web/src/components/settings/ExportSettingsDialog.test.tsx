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

const EXPORTED = {
  file: { fileName: 'termforge-settings-2026-10-27.tfsettings', contentBase64: 'VEZWVAAB' },
  key: '7K3M-9QX2-HV4T-PW8R-ZB6D-NJ5C-FG05-AY1E',
  secretCount: 2,
};

beforeEach(() => {
  mocks.getTransferSummary.mockReset().mockResolvedValue({ connections: 6, groups: 2, pinnedCommands: 14 });
  mocks.exportSettings.mockReset().mockResolvedValue(EXPORTED);
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
  it('shows what each section contains and keeps pinned commands off by default', async () => {
    renderDialog();

    expect(await screen.findByText('6 servers · 2 groups')).toBeInTheDocument();
    expect(screen.getByText('14 commands')).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: /Server connections/ })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: /Pinned commands/ })).not.toBeChecked();
    expect(screen.getByText('encrypted · with saved passwords')).toBeInTheDocument();
    expect(screen.queryByLabelText(/password/i)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Export' })).toBeEnabled();
  });

  it('saves one encrypted file and shows the key to open it', async () => {
    const user = userEvent.setup();
    const { saveFile, onExported } = renderDialog();

    await user.click(screen.getByRole('button', { name: 'Export' }));

    expect(mocks.flushWorkspaceState).toHaveBeenCalled();
    expect(mocks.exportSettings).toHaveBeenCalledWith({
      sections: { connections: true, preferences: true, workspace: true, pinnedCommands: false },
    });
    expect(saveFile).toHaveBeenCalledTimes(1);
    const [fileName, fileBytes, fileType] = saveFile.mock.calls[0] as [string, Uint8Array, string];
    expect(fileName).toBe(EXPORTED.file.fileName);
    expect(Array.from(fileBytes.slice(0, 4))).toEqual([0x54, 0x46, 0x56, 0x54]);
    expect(fileType).toBe('application/octet-stream');
    expect(await screen.findByLabelText('Export key')).toHaveTextContent(EXPORTED.key);
    expect(onExported).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: 'Copy key' }));
    expect(await navigator.clipboard.readText()).toBe(EXPORTED.key);
    expect(await screen.findByRole('button', { name: 'Copied' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Done' }));
    expect(onExported).toHaveBeenCalledWith([EXPORTED.file.fileName]);
  });

  it('disables the export when no section is chosen', async () => {
    const user = userEvent.setup();
    renderDialog();

    for (const name of [/Server connections/, /Preferences/, /Workspace layout/]) {
      await user.click(screen.getByRole('checkbox', { name }));
    }

    expect(screen.getByRole('button', { name: 'Export' })).toBeDisabled();
  });

  it('shows the error returned by the backend', async () => {
    const user = userEvent.setup();
    mocks.exportSettings.mockRejectedValue(new ApiError(409, { detail: 'Saved passwords could not be read: the vault is locked.' }));
    const { onExported } = renderDialog();

    await user.click(screen.getByRole('button', { name: 'Export' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('the vault is locked');
    expect(onExported).not.toHaveBeenCalled();
  });
});

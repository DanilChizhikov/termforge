import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/client';
import type {
  ConnectionImportStep,
  SettingsImportPreview,
  SettingsImportRequest,
  SettingsImportResult,
} from '../../api/settingsTransfer';
import { ImportSettingsDialog } from './ImportSettingsDialog';

const mocks = vi.hoisted(() => ({
  previewImport: vi.fn<(request: SettingsImportRequest) => Promise<SettingsImportPreview>>(),
  applyImport: vi.fn<(request: SettingsImportRequest) => Promise<SettingsImportResult>>(),
  flushWorkspaceState: vi.fn<() => Promise<void>>(),
  pauseWorkspaceAutosave: vi.fn<() => void>(),
  reloadConnections: vi.fn<() => Promise<never[]>>(),
}));

vi.mock('../../api/settingsTransfer', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/settingsTransfer')>()),
  previewImport: mocks.previewImport,
  applyImport: mocks.applyImport,
}));

vi.mock('../../state/workspacePersistence', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../state/workspacePersistence')>()),
  flushWorkspaceState: mocks.flushWorkspaceState,
  pauseWorkspaceAutosave: mocks.pauseWorkspaceAutosave,
}));

vi.mock('../../state/connections', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../state/connections')>()),
  reloadConnections: mocks.reloadConnections,
}));

const SETTINGS_BYTES = new Uint8Array([0x54, 0x46, 0x56, 0x54]);
const SETTINGS_BASE64 = 'VEZWVA==';
const EXPORT_KEY = '7K3M-9QX2-HV4T-PW8R-ZB6D-NJ5C-FG05-AY1E';
const ALL = { connections: true, preferences: true, workspace: true, pinnedCommands: true };

function step(overrides: Partial<ConnectionImportStep>): ConnectionImportStep {
  return {
    importedId: 'web-id',
    name: 'web',
    host: 'web.example.test',
    userName: 'deploy',
    port: 22,
    action: 'add',
    resolution: null,
    localName: 'web',
    changes: [],
    hasPassword: false,
    hasSudoPassword: false,
    ...overrides,
  };
}

function preview(overrides: Partial<SettingsImportPreview> = {}): SettingsImportPreview {
  return {
    exportedAt: '2026-10-25T10:00:00Z',
    sectionsInFile: ALL,
    sectionsToApply: ALL,
    connections: [
      step({ importedId: 'web-id', name: 'web', action: 'conflict', resolution: 'keepExisting', changes: ['host'] }),
      step({ importedId: 'db-id', name: 'db', host: 'db.example.test', localName: 'db' }),
    ],
    removals: [],
    preferenceChanges: ['Recordings kept: 50 → 10'],
    workspace: { terminalTabs: 2, editorTabs: 1, remotePaths: 3 },
    pinnedCommands: 4,
    secrets: { passwords: 0, sudoPasswords: 0 },
    warnings: [],
    ...overrides,
  };
}

function result(overrides: Partial<SettingsImportResult> = {}): SettingsImportResult {
  return {
    added: 1,
    updated: 0,
    unchanged: 0,
    kept: 1,
    removed: 0,
    secretsStored: 0,
    preferencesImported: true,
    workspaceImported: false,
    pinnedCommandsAdded: 4,
    pinnedCommandsSkipped: 0,
    warnings: [],
    ...overrides,
  };
}

beforeEach(() => {
  mocks.previewImport.mockReset().mockResolvedValue(preview());
  mocks.applyImport.mockReset().mockResolvedValue(result());
  mocks.flushWorkspaceState.mockReset().mockResolvedValue(undefined);
  mocks.pauseWorkspaceAutosave.mockReset();
  mocks.reloadConnections.mockReset().mockResolvedValue([]);
});

async function chooseSettingsFile(user: ReturnType<typeof userEvent.setup>, key = EXPORT_KEY) {
  const file = new File([SETTINGS_BYTES], 'termforge-settings-2026-10-25.tfsettings');
  await user.upload(screen.getByLabelText('Settings file'), file);
  await user.type(screen.getByLabelText('Key'), key);
  await waitFor(() => expect(screen.getByRole('button', { name: 'Preview import' })).toBeEnabled());
}

describe('ImportSettingsDialog', () => {
  it('previews the changes before anything is imported', async () => {
    const user = userEvent.setup();
    render(<ImportSettingsDialog onClose={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'Preview import' })).toBeDisabled();
    await chooseSettingsFile(user);
    await user.click(screen.getByRole('button', { name: 'Preview import' }));

    expect(mocks.previewImport).toHaveBeenCalledWith(
      expect.objectContaining({ file: SETTINGS_BASE64, key: EXPORT_KEY, mode: 'merge' }),
    );
    expect(mocks.applyImport).not.toHaveBeenCalled();
    const changes = await screen.findByRole('list', { name: 'Server changes' });
    expect(within(changes).getByText('Conflict')).toBeInTheDocument();
    expect(within(changes).getByText('Differs: host')).toBeInTheDocument();
    expect(within(changes).getByText('New')).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Resolve web' })).toHaveValue('keepExisting');
    expect(screen.getByText('Recordings kept: 50 → 10')).toBeInTheDocument();
  });

  it('sends the chosen conflict resolutions with the import', async () => {
    const user = userEvent.setup();
    render(<ImportSettingsDialog onClose={vi.fn()} />);
    await chooseSettingsFile(user);
    await user.click(screen.getByRole('button', { name: 'Preview import' }));

    await user.selectOptions(await screen.findByRole('combobox', { name: 'Resolve web' }), 'keepBoth');
    await user.click(screen.getByRole('button', { name: 'Import' }));

    expect(mocks.flushWorkspaceState).toHaveBeenCalled();
    expect(mocks.applyImport).toHaveBeenCalledWith(expect.objectContaining({ resolutions: { 'web-id': 'keepBoth' } }));
    expect(await screen.findByRole('list', { name: 'Import result' })).toHaveTextContent('Servers: 1 added, 1 kept as they were.');
    expect(mocks.reloadConnections).toHaveBeenCalled();
    expect(mocks.pauseWorkspaceAutosave).not.toHaveBeenCalled();
  });

  it('re-runs the preview without a section when it is unchecked', async () => {
    const user = userEvent.setup();
    render(<ImportSettingsDialog onClose={vi.fn()} />);
    await chooseSettingsFile(user);
    await user.click(screen.getByRole('button', { name: 'Preview import' }));

    await user.click(await screen.findByRole('checkbox', { name: /Pinned commands/ }));

    expect(mocks.previewImport).toHaveBeenLastCalledWith(
      expect.objectContaining({ sections: { ...ALL, pinnedCommands: false } }),
    );
  });

  it('asks for confirmation before replacing removes servers', async () => {
    const user = userEvent.setup();
    mocks.previewImport.mockResolvedValue(
      preview({ removals: [{ id: 'old-id', name: 'old-vps', host: '203.0.113.9' }], connections: [step({ action: 'update' })] }),
    );
    render(<ImportSettingsDialog onClose={vi.fn()} />);
    await chooseSettingsFile(user);
    await user.click(screen.getByRole('radio', { name: /Replace my settings/ }));
    await user.click(screen.getByRole('button', { name: 'Preview import' }));

    const replaceButton = await screen.findByRole('button', { name: 'Replace settings' });
    expect(within(screen.getByRole('list', { name: 'Server changes' })).getByText('Remove')).toBeInTheDocument();
    expect(replaceButton).toBeDisabled();
    expect(replaceButton).toHaveClass('btn-destructive');

    await user.click(screen.getByRole('checkbox', { name: /Remove 1 server that is not in the file/ }));
    await user.click(replaceButton);

    expect(mocks.applyImport).toHaveBeenCalledWith(expect.objectContaining({ mode: 'replace', confirmReplace: true }));
  });

  it('requires both the settings file and the key before previewing', async () => {
    const user = userEvent.setup();
    render(<ImportSettingsDialog onClose={vi.fn()} />);

    const file = new File([SETTINGS_BYTES], 'termforge-settings-2026-10-25.tfsettings');
    await user.upload(screen.getByLabelText('Settings file'), file);

    expect(screen.getByLabelText('Settings file')).toHaveAttribute('accept', '.tfsettings');
    expect(screen.getByRole('button', { name: 'Preview import' })).toBeDisabled();
    await user.type(screen.getByLabelText('Key'), EXPORT_KEY);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Preview import' })).toBeEnabled());
  });

  it('shows a wrong key error', async () => {
    const user = userEvent.setup();
    mocks.previewImport.mockRejectedValue(new ApiError(400, { detail: 'The key is incorrect.', code: 'WrongKey' }));
    render(<ImportSettingsDialog onClose={vi.fn()} />);
    await chooseSettingsFile(user, '0000-0000-0000-0000-0000-0000-0000-0000');

    await user.click(screen.getByRole('button', { name: 'Preview import' }));

    expect(mocks.previewImport).toHaveBeenCalledWith(
      expect.objectContaining({ file: SETTINGS_BASE64, key: '0000-0000-0000-0000-0000-0000-0000-0000' }),
    );
    expect(await screen.findByRole('alert')).toHaveTextContent('The key is incorrect.');
  });

  it('shows how many saved passwords the file brings', async () => {
    const user = userEvent.setup();
    mocks.previewImport.mockResolvedValue(preview({ secrets: { passwords: 2, sudoPasswords: 1 } }));
    render(<ImportSettingsDialog onClose={vi.fn()} />);
    await chooseSettingsFile(user);

    await user.click(screen.getByRole('button', { name: 'Preview import' }));

    expect(await screen.findByText(/3 passwords/)).toBeInTheDocument();
  });

  it('pauses workspace autosave and offers a reload after importing the workspace', async () => {
    const user = userEvent.setup();
    const reloadPage = vi.fn();
    mocks.applyImport.mockResolvedValue(result({ workspaceImported: true, warnings: ["'web' signs in with a password."] }));
    render(<ImportSettingsDialog onClose={vi.fn()} reloadPage={reloadPage} />);
    await chooseSettingsFile(user);
    await user.click(screen.getByRole('button', { name: 'Preview import' }));
    await user.click(await screen.findByRole('button', { name: 'Import' }));

    expect(mocks.pauseWorkspaceAutosave).toHaveBeenCalled();
    expect(await screen.findByRole('list', { name: 'Import warnings' })).toHaveTextContent('signs in with a password');
    expect(screen.queryByRole('button', { name: 'Close' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Reload TermForge' }));
    expect(reloadPage).toHaveBeenCalled();
  });
});

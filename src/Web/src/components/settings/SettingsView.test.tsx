import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { RecordingSettings, RecordingSettingsResponse, RecordingSummary } from '../../api/recordings';
import { SettingsView } from './SettingsView';

const mocks = vi.hoisted(() => ({
  getRecordingSettings: vi.fn<() => Promise<RecordingSettingsResponse>>(),
  updateRecordingSettings: vi.fn<(settings: RecordingSettings) => Promise<RecordingSettingsResponse>>(),
  listRecordings: vi.fn<() => Promise<RecordingSummary[]>>(),
  deleteRecording: vi.fn<(id: string) => Promise<void>>(),
}));

vi.mock('../../api/recordings', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/recordings')>()),
  getRecordingSettings: mocks.getRecordingSettings,
  updateRecordingSettings: mocks.updateRecordingSettings,
  listRecordings: mocks.listRecordings,
  deleteRecording: mocks.deleteRecording,
}));

const DEFAULTS: RecordingSettingsResponse = {
  enabled: false,
  maxRecordingSizeMegabytes: 10,
  maxRecordings: 50,
  retentionDays: 30,
  directory: '/home/me/.local/share/termforge/recordings',
};

function recording(overrides: Partial<RecordingSummary> = {}): RecordingSummary {
  return {
    id: '0123456789abcdef0123456789abcdef',
    connectionId: 'web-id',
    connectionName: 'web',
    sessionNumber: 1,
    columns: 80,
    rows: 24,
    startedAt: '2026-10-26T10:00:00Z',
    endedAt: '2026-10-26T10:01:30Z',
    sizeBytes: 2048,
    status: 'complete',
    endReason: 'stopped',
    ...overrides,
  };
}

beforeEach(() => {
  mocks.getRecordingSettings.mockReset().mockResolvedValue(DEFAULTS);
  mocks.updateRecordingSettings.mockReset().mockImplementation((settings) => Promise.resolve({ ...DEFAULTS, ...settings }));
  mocks.listRecordings.mockReset().mockResolvedValue([]);
  mocks.deleteRecording.mockReset().mockResolvedValue(undefined);
});

describe('SettingsView recording settings', () => {
  it('shows terminal recording as off by default', async () => {
    render(<SettingsView />);

    const toggle = await screen.findByRole('switch', { name: 'Record terminal output' });
    expect(toggle).toHaveAttribute('aria-checked', 'false');
    expect(screen.getByText('/home/me/.local/share/termforge/recordings')).toBeInTheDocument();
    expect(await screen.findByText('No recordings yet.')).toBeInTheDocument();
  });

  it('warns that recordings may contain secrets before turning recording on', async () => {
    const user = userEvent.setup();
    render(<SettingsView />);

    await user.click(await screen.findByRole('switch', { name: 'Record terminal output' }));

    const dialog = screen.getByRole('dialog', { name: 'Turn on terminal recording?' });
    expect(within(dialog).getByTestId('recording-secrets-warning')).toHaveTextContent(/may contain secrets/);
    expect(mocks.updateRecordingSettings).not.toHaveBeenCalled();
    expect(within(dialog).getByRole('button', { name: 'Cancel' })).toHaveFocus();

    await user.click(within(dialog).getByRole('button', { name: 'Turn on recording' }));

    expect(mocks.updateRecordingSettings).toHaveBeenCalledWith({
      enabled: true,
      maxRecordingSizeMegabytes: 10,
      maxRecordings: 50,
      retentionDays: 30,
    });
    expect(await screen.findByRole('switch', { name: 'Record terminal output' })).toHaveAttribute('aria-checked', 'true');
  });

  it('keeps recording off when the warning is cancelled', async () => {
    const user = userEvent.setup();
    render(<SettingsView />);

    await user.click(await screen.findByRole('switch', { name: 'Record terminal output' }));
    await user.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(mocks.updateRecordingSettings).not.toHaveBeenCalled();
    expect(screen.getByRole('switch', { name: 'Record terminal output' })).toHaveAttribute('aria-checked', 'false');
  });

  it('saves size and retention limits and rejects values outside the limits', async () => {
    const user = userEvent.setup();
    render(<SettingsView />);
    const size = await screen.findByRole('textbox', { name: 'Maximum size per recording in MB' });
    const save = screen.getByRole('button', { name: 'Save limits' });
    expect(save).toBeDisabled();

    await user.clear(size);
    await user.type(size, '500');
    expect(save).toBeDisabled();

    await user.clear(size);
    await user.type(size, '25');
    await user.clear(screen.getByRole('textbox', { name: 'Days to keep recordings' }));
    await user.type(screen.getByRole('textbox', { name: 'Days to keep recordings' }), '7');
    await user.click(save);

    expect(mocks.updateRecordingSettings).toHaveBeenCalledWith({
      enabled: false,
      maxRecordingSizeMegabytes: 25,
      maxRecordings: 50,
      retentionDays: 7,
    });
  });
});

describe('SettingsView recordings list', () => {
  it('lists recordings with status words', async () => {
    mocks.listRecordings.mockResolvedValue([
      recording(),
      recording({ id: 'ffffffffffffffffffffffffffffffff', sessionNumber: 2, status: 'truncated', endReason: 'sizeLimitReached' }),
    ]);
    render(<SettingsView />);

    const table = await screen.findByRole('table');
    expect(within(table).getByText('web #1')).toBeInTheDocument();
    expect(within(table).getByText('Complete')).toBeInTheDocument();
    expect(within(table).getByText('Truncated')).toBeInTheDocument();
    expect(within(table).getAllByText('1:30')).toHaveLength(2);
    expect(within(table).getAllByText('2.0 KB')).toHaveLength(2);
  });

  it('deletes a recording only after confirmation', async () => {
    const user = userEvent.setup();
    mocks.listRecordings.mockResolvedValue([recording()]);
    render(<SettingsView />);

    await user.click(await screen.findByRole('button', { name: 'Delete web #1' }));
    const dialog = screen.getByRole('dialog');
    expect(within(dialog).getByRole('button', { name: 'Cancel' })).toHaveFocus();
    expect(mocks.deleteRecording).not.toHaveBeenCalled();

    await user.click(within(dialog).getByRole('button', { name: 'Delete recording' }));

    expect(mocks.deleteRecording).toHaveBeenCalledWith('0123456789abcdef0123456789abcdef');
    expect(await screen.findByText('No recordings yet.')).toBeInTheDocument();
  });

  it('does not offer deleting a recording in progress', async () => {
    mocks.listRecordings.mockResolvedValue([recording({ status: 'recording', endedAt: null, endReason: null })]);
    render(<SettingsView />);

    expect(await screen.findByRole('button', { name: 'Delete web #1' })).toBeDisabled();
    expect(screen.getByText('Recording')).toBeInTheDocument();
  });
});

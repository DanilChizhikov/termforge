import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { SessionRecording } from '../api/recordings';
import { showAppView, useAppView } from '../state/appView';
import { TerminalRecordingBar } from './TerminalRecordingBar';

const mocks = vi.hoisted(() => ({
  getSessionRecording: vi.fn<(id: string) => Promise<SessionRecording>>(),
  startSessionRecording: vi.fn<(id: string) => Promise<SessionRecording>>(),
  stopSessionRecording: vi.fn<(id: string) => Promise<SessionRecording>>(),
}));

vi.mock('../api/recordings', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/recordings')>()),
  getSessionRecording: mocks.getSessionRecording,
  startSessionRecording: mocks.startSessionRecording,
  stopSessionRecording: mocks.stopSessionRecording,
}));

function status(overrides: Partial<SessionRecording> = {}): SessionRecording {
  return {
    recording: false,
    available: true,
    recordingId: null,
    sizeBytes: 0,
    maxBytes: 0,
    status: null,
    endReason: null,
    ...overrides,
  };
}

const RECORDING = status({
  recording: true,
  recordingId: '0123456789abcdef0123456789abcdef',
  sizeBytes: 1536,
  maxBytes: 10 * 1024 * 1024,
  status: 'recording',
});

function ViewProbe() {
  return <span data-testid="view">{useAppView()}</span>;
}

beforeEach(() => {
  act(() => showAppView('workspace'));
  mocks.getSessionRecording.mockReset().mockResolvedValue(status());
  mocks.startSessionRecording.mockReset().mockResolvedValue(RECORDING);
  mocks.stopSessionRecording.mockReset().mockResolvedValue(status({ status: 'complete', endReason: 'stopped' }));
});

describe('TerminalRecordingBar', () => {
  it('is off by default and points to Settings while recording is not allowed', async () => {
    const user = userEvent.setup();
    mocks.getSessionRecording.mockResolvedValue(status({ available: false }));
    render(
      <>
        <TerminalRecordingBar terminalId="term-1" onRecordingChange={() => undefined} />
        <ViewProbe />
      </>,
    );

    expect(screen.getByText('Recording off')).toBeInTheDocument();
    await user.click(await screen.findByRole('button', { name: 'Turn on in Settings' }));

    expect(screen.getByTestId('view')).toHaveTextContent('settings');
    expect(mocks.startSessionRecording).not.toHaveBeenCalled();
  });

  it('starts recording this session only after the secrets warning is accepted', async () => {
    const user = userEvent.setup();
    const onRecordingChange = vi.fn();
    render(<TerminalRecordingBar terminalId="term-1" onRecordingChange={onRecordingChange} />);

    await user.click(await screen.findByRole('button', { name: 'Record' }));
    const dialog = screen.getByRole('dialog', { name: 'Record this terminal?' });
    expect(within(dialog).getByTestId('recording-secrets-warning')).toHaveTextContent(/may contain secrets/);
    expect(mocks.startSessionRecording).not.toHaveBeenCalled();

    await user.click(within(dialog).getByRole('button', { name: 'Start recording' }));

    expect(mocks.startSessionRecording).toHaveBeenCalledWith('term-1');
    expect(await screen.findByText(/Recording · 1\.5 KB of 10\.0 MB/)).toBeInTheDocument();
    expect(onRecordingChange).toHaveBeenLastCalledWith(true);
  });

  it('stops the recording', async () => {
    const user = userEvent.setup();
    const onRecordingChange = vi.fn();
    mocks.getSessionRecording.mockResolvedValue(RECORDING);
    render(<TerminalRecordingBar terminalId="term-1" onRecordingChange={onRecordingChange} />);

    await user.click(await screen.findByRole('button', { name: 'Stop recording' }));

    expect(mocks.stopSessionRecording).toHaveBeenCalledWith('term-1');
    expect(await screen.findByText('Recording off')).toBeInTheDocument();
    expect(onRecordingChange).toHaveBeenLastCalledWith(false);
  });

  it('says when the recording stopped at the size limit', async () => {
    mocks.getSessionRecording.mockResolvedValue(status({ status: 'truncated', endReason: 'sizeLimitReached' }));
    render(<TerminalRecordingBar terminalId="term-1" onRecordingChange={() => undefined} />);

    expect(await screen.findByText('Recording stopped at the size limit')).toBeInTheDocument();
  });
});

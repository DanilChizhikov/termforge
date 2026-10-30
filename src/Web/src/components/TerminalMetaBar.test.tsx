import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { SavedConnection } from '../api/connections';
import type { SessionRecording } from '../api/recordings';
import { replaceConnections } from '../state/connections';
import { TerminalMetaBar } from './TerminalMetaBar';

const mocks = vi.hoisted(() => ({
  getSessionRecording: vi.fn<(id: string) => Promise<SessionRecording>>(),
}));

vi.mock('../api/recordings', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/recordings')>()),
  getSessionRecording: mocks.getSessionRecording,
}));

const CONNECTION: SavedConnection = {
  id: 'c1',
  name: 'web-01',
  groupName: null,
  host: '203.0.113.24',
  port: 22,
  userName: 'deploy',
  authMethod: 'Agent',
  privateKeyPath: null,
  jumpHostId: null,
  sshAlias: null,
  hasPassword: false,
  lastConnectedAt: null,
  reconnectMode: 'Manual',
  reconnectMaxAttempts: 5,
  reconnectInitialDelaySeconds: 2,
  reconnectMaxDelaySeconds: 60,
  readOnly: false,
  aiAccess: false,
};

beforeEach(() => {
  mocks.getSessionRecording.mockReset().mockResolvedValue({
    recording: false,
    available: true,
    recordingId: null,
    sizeBytes: 0,
    maxBytes: 0,
    status: null,
    endReason: null,
  });
  replaceConnections([CONNECTION]);
});

describe('TerminalMetaBar', () => {
  it('shows who is connected, for how long, the terminal size and the recording state', async () => {
    const openedAt = new Date(Date.now() - (2 * 60 + 14) * 60_000).toISOString();

    render(
      <TerminalMetaBar
        session={{ id: 't1', connectionId: 'c1', connectionName: 'web-01', sessionNumber: 1, openedAt }}
        size={{ columns: 132, rows: 24 }}
        onRecordingChange={() => undefined}
      />,
    );

    expect(screen.getByText('deploy@203.0.113.24')).toBeInTheDocument();
    expect(screen.getByText('Connected 2 h 14 m')).toBeInTheDocument();
    expect(screen.getByText('132×24')).toBeInTheDocument();
    expect(await screen.findByText('Recording off')).toBeInTheDocument();
  });
});

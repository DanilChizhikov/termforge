import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { AiBridgeSettings } from '../../api/aiBridge';
import { AiBridgeSection } from './AiBridgeSection';

const mocks = vi.hoisted(() => ({
  getAiBridgeSettings: vi.fn(),
  updateAiBridgeSettings: vi.fn(),
  regenerateAiBridgeToken: vi.fn(),
}));

vi.mock('../../api/aiBridge', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/aiBridge')>()),
  getAiBridgeSettings: mocks.getAiBridgeSettings,
  updateAiBridgeSettings: mocks.updateAiBridgeSettings,
  regenerateAiBridgeToken: mocks.regenerateAiBridgeToken,
}));

const DEFAULTS: AiBridgeSettings = {
  enabled: false,
  requireApproval: true,
  hasToken: false,
  mcpUrl: 'http://127.0.0.1:5023/mcp',
};

describe('AiBridgeSection', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getAiBridgeSettings.mockResolvedValue(DEFAULTS);
  });

  it('is off by default, asks before actions and shows the endpoint', async () => {
    render(<AiBridgeSection />);

    expect(await screen.findByRole('switch', { name: 'Enable AI bridge' })).toHaveAttribute('aria-checked', 'false');
    expect(screen.getByRole('switch', { name: 'Ask before AI actions' })).toHaveAttribute('aria-checked', 'true');
    expect(screen.getByText('http://127.0.0.1:5023/mcp')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Generate token' })).toBeInTheDocument();
  });

  it('turns the bridge on and keeps approvals', async () => {
    const user = userEvent.setup();
    mocks.updateAiBridgeSettings.mockResolvedValue({ ...DEFAULTS, enabled: true });
    render(<AiBridgeSection />);

    await user.click(await screen.findByRole('switch', { name: 'Enable AI bridge' }));

    expect(mocks.updateAiBridgeSettings).toHaveBeenCalledWith({ enabled: true, requireApproval: true });
    expect(await screen.findByRole('switch', { name: 'Enable AI bridge' })).toHaveAttribute('aria-checked', 'true');
  });

  it('turns approvals off', async () => {
    const user = userEvent.setup();
    mocks.updateAiBridgeSettings.mockResolvedValue({ ...DEFAULTS, requireApproval: false });
    render(<AiBridgeSection />);

    await user.click(await screen.findByRole('switch', { name: 'Ask before AI actions' }));

    expect(mocks.updateAiBridgeSettings).toHaveBeenCalledWith({ enabled: false, requireApproval: false });
  });

  it('shows a new token once with the Claude Code command', async () => {
    const user = userEvent.setup();
    mocks.regenerateAiBridgeToken.mockResolvedValue({ token: 'secret-token', mcpUrl: DEFAULTS.mcpUrl });
    render(<AiBridgeSection />);

    await user.click(await screen.findByRole('button', { name: 'Generate token' }));

    expect(await screen.findByLabelText('Access token')).toHaveTextContent('secret-token');
    expect(screen.getByLabelText('Claude Code command')).toHaveTextContent(
      'claude mcp add --transport http termforge http://127.0.0.1:5023/mcp --header "Authorization: Bearer secret-token"',
    );
    expect(screen.getByRole('button', { name: 'Replace token' })).toBeInTheDocument();
  });
});

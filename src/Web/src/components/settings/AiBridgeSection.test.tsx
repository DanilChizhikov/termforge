import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { AiBridgeSettings } from '../../api/aiBridge';
import { AiBridgeSection } from './AiBridgeSection';
import { ApiError } from '../../api/client';

const mocks = vi.hoisted(() => ({
  getAiBridgeSettings: vi.fn(),
  updateAiBridgeSettings: vi.fn(),
  listAiBridgeTokens: vi.fn(),
  createAiBridgeToken: vi.fn(),
  revokeAiBridgeToken: vi.fn(),
}));

vi.mock('../../api/aiBridge', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/aiBridge')>()),
  getAiBridgeSettings: mocks.getAiBridgeSettings,
  updateAiBridgeSettings: mocks.updateAiBridgeSettings,
  listAiBridgeTokens: mocks.listAiBridgeTokens,
  createAiBridgeToken: mocks.createAiBridgeToken,
  revokeAiBridgeToken: mocks.revokeAiBridgeToken,
}));

const DEFAULTS: AiBridgeSettings = {
  enabled: false,
  requireApproval: true,
  hasToken: false,
  mcpUrl: 'http://127.0.0.1:5023/mcp',
};

const TOKEN = { id: 'token-id', name: 'Agent', createdAt: '2026-11-07T01:00:00Z' };

async function createToken(user: ReturnType<typeof userEvent.setup>) {
  await user.type(await screen.findByRole('textbox', { name: 'Token name' }), '  Agent  ');
  await user.click(screen.getByRole('button', { name: 'Create token' }));
  await screen.findByLabelText('Access token');
}

describe('AiBridgeSection', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getAiBridgeSettings.mockResolvedValue(DEFAULTS);
    mocks.listAiBridgeTokens.mockResolvedValue([]);
    mocks.createAiBridgeToken.mockResolvedValue({ ...TOKEN, token: 'secret-token', mcpUrl: DEFAULTS.mcpUrl });
    mocks.revokeAiBridgeToken.mockResolvedValue(undefined);
  });

  it('is off by default, asks before actions and shows the endpoint', async () => {
    render(<AiBridgeSection />);

    expect(await screen.findByRole('switch', { name: 'Enable AI bridge' })).toHaveAttribute('aria-checked', 'false');
    expect(screen.getByRole('switch', { name: 'Ask before AI actions' })).toHaveAttribute('aria-checked', 'true');
    expect(screen.getByText('http://127.0.0.1:5023/mcp')).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'Create token' })).toBeDisabled();
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
    render(<AiBridgeSection />);

    await createToken(user);

    expect(mocks.createAiBridgeToken).toHaveBeenCalledWith('Agent');
    expect(await screen.findByLabelText('Access token')).toHaveTextContent('secret-token');
    expect(screen.getByLabelText('Claude Code command')).toHaveTextContent(
      'claude mcp add --transport http termforge http://127.0.0.1:5023/mcp --header "Authorization: Bearer secret-token"',
    );
    expect(screen.getByRole('button', { name: 'Revoke Agent' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Close token' }));
    expect(screen.queryByLabelText('Access token')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Claude Code command')).not.toBeInTheDocument();
  });

  it('copies the token and command', async () => {
    const user = userEvent.setup();
    render(<AiBridgeSection />);
    await createToken(user);
    await user.click(screen.getByRole('button', { name: 'Copy token' }));
    expect(await navigator.clipboard.readText()).toBe('secret-token');
    await user.click(screen.getByRole('button', { name: 'Copy command' }));
    expect(await navigator.clipboard.readText()).toContain('Authorization: Bearer secret-token');
  });

  it('defaults to cancellation and revokes only the chosen token', async () => {
    const user = userEvent.setup();
    mocks.listAiBridgeTokens.mockResolvedValue([{ ...TOKEN, id: 'other-id', name: 'Other' }]);
    render(<AiBridgeSection />);
    await createToken(user);
    await user.click(screen.getByRole('button', { name: 'Revoke Agent' }));
    let dialog = screen.getByRole('alertdialog', { name: 'Revoke Agent?' });
    expect(within(dialog).getByRole('button', { name: 'Cancel' })).toHaveFocus();
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(mocks.revokeAiBridgeToken).not.toHaveBeenCalled();
    await user.click(screen.getByRole('button', { name: 'Revoke Agent' }));
    dialog = screen.getByRole('alertdialog');
    await user.click(within(dialog).getByRole('button', { name: 'Revoke token' }));
    expect(mocks.revokeAiBridgeToken).toHaveBeenCalledWith(TOKEN.id);
    expect(screen.queryByLabelText('Access token')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Revoke Agent' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Revoke Other' })).toBeInTheDocument();
  });

  it('does not show the secret after remounting', async () => {
    const user = userEvent.setup();
    const view = render(<AiBridgeSection />);
    await createToken(user);
    view.unmount();
    mocks.listAiBridgeTokens.mockResolvedValue([TOKEN]);
    render(<AiBridgeSection />);
    expect(await screen.findByRole('button', { name: 'Revoke Agent' })).toBeInTheDocument();
    expect(screen.queryByLabelText('Access token')).not.toBeInTheDocument();
  });

  it('shows a duplicate name error without adding a token', async () => {
    const user = userEvent.setup();
    mocks.createAiBridgeToken.mockRejectedValue(new ApiError(409, {}));
    render(<AiBridgeSection />);
    await user.type(await screen.findByRole('textbox', { name: 'Token name' }), 'Agent');
    await user.click(screen.getByRole('button', { name: 'Create token' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('A token with this name already exists.');
    expect(screen.queryByLabelText('Access token')).not.toBeInTheDocument();
  });

  it('keeps a token visible when revocation fails', async () => {
    const user = userEvent.setup();
    mocks.revokeAiBridgeToken.mockRejectedValue(new ApiError(500, {}));
    mocks.listAiBridgeTokens.mockResolvedValue([TOKEN]);
    render(<AiBridgeSection />);
    await user.click(await screen.findByRole('button', { name: 'Revoke Agent' }));
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Revoke token' }));
    expect(await within(screen.getByRole('alertdialog')).findByRole('alert')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Revoke Agent' })).toBeInTheDocument();
  });
});

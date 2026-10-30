import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { AiApproval } from '../api/aiBridge';
import { AiApprovalDialog } from './AiApprovalDialog';
import { AiApprovalPrompt } from './AiApprovalPrompt';

vi.mock('./editor/CodeDiff', () => ({
  default: ({ original, modified }: { original: string; modified: string }) => (
    <div data-testid="code-diff">
      {original}→{modified}
    </div>
  ),
}));

const mocks = vi.hoisted(() => ({
  listAiApprovals: vi.fn(),
  decideAiApproval: vi.fn(),
  sockets: [] as { onmessage: ((event: MessageEvent<string>) => void) | null; close: () => void }[],
}));

vi.mock('../api/aiBridge', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/aiBridge')>()),
  listAiApprovals: mocks.listAiApprovals,
  decideAiApproval: mocks.decideAiApproval,
}));

vi.mock('../api/webSocket', () => ({
  openWebSocket: () => {
    const socket = { onmessage: null, close: vi.fn() };
    mocks.sockets.push(socket);
    return socket;
  },
}));

function makeApproval(overrides: Partial<AiApproval> = {}): AiApproval {
  return {
    id: 'approval-1',
    connectionId: 'c1',
    serverName: 'prod-web',
    kind: 'command',
    summary: 'systemctl restart nginx',
    fileChange: null,
    expiresAt: '2026-10-30T10:02:00Z',
    ...overrides,
  };
}

describe('AiApprovalDialog', () => {
  it('shows the server and the exact command', () => {
    render(<AiApprovalDialog approval={makeApproval()} queued={1} error={null} busy={false} onDecide={vi.fn()} />);

    expect(
      screen.getByRole('alertdialog', { name: 'Allow the AI agent to run a command on prod-web?' }),
    ).toHaveAttribute('aria-modal', 'true');
    expect(screen.getByText('systemctl restart nginx')).toBeInTheDocument();
  });

  it('focuses Deny so the safe choice is the default', () => {
    render(<AiApprovalDialog approval={makeApproval()} queued={1} error={null} busy={false} onDecide={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'Deny' })).toHaveFocus();
  });

  it.each([
    ['Approve', true],
    ['Deny', false],
  ])('sends %s', async (label, approve) => {
    const onDecide = vi.fn();
    const user = userEvent.setup();
    render(<AiApprovalDialog approval={makeApproval()} queued={1} error={null} busy={false} onDecide={onDecide} />);

    await user.click(screen.getByRole('button', { name: label }));

    expect(onDecide).toHaveBeenCalledWith(approve);
  });

  it('denies on Escape', async () => {
    const onDecide = vi.fn();
    const user = userEvent.setup();
    render(<AiApprovalDialog approval={makeApproval()} queued={1} error={null} busy={false} onDecide={onDecide} />);

    await user.keyboard('{Escape}');

    expect(onDecide).toHaveBeenCalledWith(false);
  });

  it('shows a diff for file writes', async () => {
    const approval = makeApproval({
      kind: 'fileWrite',
      summary: 'Edit /srv/app/.env',
      fileChange: { path: '/srv/app/.env', before: 'A=1', after: 'A=2' },
    });
    render(<AiApprovalDialog approval={approval} queued={3} error={null} busy={false} onDecide={vi.fn()} />);

    expect(screen.getByRole('alertdialog', { name: /write a file on prod-web/ })).toBeInTheDocument();
    expect(screen.getByText('/srv/app/.env')).toBeInTheDocument();
    expect(await screen.findByTestId('code-diff')).toHaveTextContent('A=1→A=2');
    expect(screen.getByText(/2 more waiting/)).toBeInTheDocument();
  });
});

describe('AiApprovalPrompt', () => {
  beforeEach(() => {
    mocks.sockets.length = 0;
    mocks.listAiApprovals.mockResolvedValue([]);
    mocks.decideAiApproval.mockResolvedValue(undefined);
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('opens when a request arrives and sends the decision', async () => {
    const user = userEvent.setup();
    render(<AiApprovalPrompt />);
    await waitFor(() => expect(mocks.listAiApprovals).toHaveBeenCalled());
    await act(async () => {});
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();

    const envelope = { type: 'aiBridge.approvalsChanged', data: [makeApproval()] };
    act(() => {
      mocks.sockets[0]?.onmessage?.(new MessageEvent('message', { data: JSON.stringify(envelope) }));
    });
    await user.click(await screen.findByRole('button', { name: 'Approve' }));

    expect(mocks.decideAiApproval).toHaveBeenCalledWith('approval-1', true);
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
  });

  it('shows pending requests on load', async () => {
    mocks.listAiApprovals.mockResolvedValue([makeApproval()]);

    render(<AiApprovalPrompt />);

    expect(await screen.findByText('systemctl restart nginx')).toBeInTheDocument();
  });
});

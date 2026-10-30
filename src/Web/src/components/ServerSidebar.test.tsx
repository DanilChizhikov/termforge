import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import * as api from '../api/connections';
import type { SavedConnection } from '../api/connections';
import { ApiError } from '../api/client';
import { Group, Panel } from 'react-resizable-panels';
import { resetWorkspaceLayout, restoreWorkspaceLayout } from '../state/workspaceLayout';
import { CollapsibleRegion } from './CollapsibleRegion';
import { selectServer } from '../state/selectedServer';
import { ServerSidebar } from './ServerSidebar';

vi.mock('../api/connections', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api/connections')>();
  return {
    ...actual,
    listConnections: vi.fn(),
    createConnection: vi.fn(),
    updateConnection: vi.fn(),
    removeConnection: vi.fn(),
    testConnection: vi.fn(),
    importSshConfig: vi.fn(),
    renameGroup: vi.fn(),
    removeGroup: vi.fn(),
  };
});

function makeConnection(overrides: Partial<SavedConnection> = {}): SavedConnection {
  return {
    id: 'c1',
    name: 'web',
    groupName: null,
    host: 'example.org',
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
    readOnly: false,
    aiAccess: false,
    reconnectMaxDelaySeconds: 60,
    ...overrides,
  };
}

const listMock = vi.mocked(api.listConnections);

function renderInRegion() {
  return render(
    <Group orientation="horizontal" id="sidebar-test">
      <CollapsibleRegion id="servers" title="Servers" defaultSize="30%" minSize="10%" collapsedLayout="content">
        <ServerSidebar />
      </CollapsibleRegion>
      <Panel id="rest" minSize="30%" />
    </Group>,
  );
}

beforeEach(() => {
  resetWorkspaceLayout();
  selectServer(null);
  listMock.mockResolvedValue([]);
});

afterEach(() => {
  vi.clearAllMocks();
});

describe('ServerSidebar', () => {
  it('shows the empty state without servers', async () => {
    render(<ServerSidebar />);

    expect(await screen.findByText('No servers yet.')).toBeInTheDocument();
  });

  it('lists servers by group with an ungrouped section', async () => {
    listMock.mockResolvedValue([
      makeConnection({ id: 'a', name: 'alpha', groupName: 'prod' }),
      makeConnection({ id: 'b', name: 'beta' }),
    ]);

    render(<ServerSidebar />);

    const prod = await screen.findByRole('region', { name: 'Group prod' });
    expect(within(prod).getByText(/alpha/)).toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: 'Ungrouped' })).getByText(/beta/)).toBeInTheDocument();
  });

  it('marks read-only servers with a badge', async () => {
    listMock.mockResolvedValue([
      makeConnection({ id: 'a', name: 'db-01', readOnly: true }),
      makeConnection({ id: 'b', name: 'web-01' }),
    ]);

    render(<ServerSidebar />);

    expect(await screen.findByRole('button', { name: 'db-01, read-only' })).toBeInTheDocument();
    expect(screen.getAllByText('read-only')).toHaveLength(1);
  });

  it('stores the selected server in UI state', async () => {
    const user = userEvent.setup();
    listMock.mockResolvedValue([makeConnection()]);
    render(<ServerSidebar />);

    const button = await screen.findByRole('button', { name: /web/, pressed: false });
    await user.click(button);

    expect(screen.getByRole('button', { name: /web/, pressed: true })).toBeInTheDocument();
  });

  it('creates a server from the add dialog', async () => {
    const user = userEvent.setup();
    vi.mocked(api.createConnection).mockResolvedValue(makeConnection());
    render(<ServerSidebar />);

    await user.click(screen.getByRole('button', { name: 'Add server' }));
    const dialog = screen.getByRole('dialog', { name: 'Add server' });
    await user.type(within(dialog).getByLabelText('Name'), 'web');
    await user.type(within(dialog).getByLabelText('Host or SSH alias'), 'example.org');
    await user.type(within(dialog).getByLabelText('User'), 'deploy');
    await user.click(within(dialog).getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(api.createConnection).toHaveBeenCalledTimes(1));
    expect(vi.mocked(api.createConnection).mock.calls[0]?.[0]).toMatchObject({
      name: 'web',
      host: 'example.org',
      userName: 'deploy',
      port: 22,
      authMethod: 'Agent',
    });
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('closes the add dialog with the close button and with Escape', async () => {
    const user = userEvent.setup();
    render(<ServerSidebar />);

    await user.click(screen.getByRole('button', { name: 'Add server' }));
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Close' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Add server' }));
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('shows the password field only for password authentication', async () => {
    const user = userEvent.setup();
    render(<ServerSidebar />);
    await user.click(screen.getByRole('button', { name: 'Add server' }));
    const dialog = screen.getByRole('dialog');

    expect(within(dialog).getByRole('radio', { name: 'SSH agent' })).toBeChecked();
    expect(within(dialog).queryByLabelText('Server password')).not.toBeInTheDocument();
    await user.click(within(dialog).getByRole('radio', { name: 'Password' }));

    expect(within(dialog).getByLabelText('Server password')).toHaveAttribute('type', 'password');
  });

  it('shows the server error inside the dialog and keeps it open', async () => {
    const user = userEvent.setup();
    vi.mocked(api.createConnection).mockRejectedValue(
      new ApiError(409, { detail: 'A saved connection named web already exists.' }),
    );
    render(<ServerSidebar />);

    await user.click(screen.getByRole('button', { name: 'Add server' }));
    const dialog = screen.getByRole('dialog');
    await user.type(within(dialog).getByLabelText('Name'), 'web');
    await user.type(within(dialog).getByLabelText('Host or SSH alias'), 'h');
    await user.type(within(dialog).getByLabelText('User'), 'u');
    await user.click(within(dialog).getByRole('button', { name: 'Save' }));

    expect(await within(dialog).findByText(/already exists/)).toBeInTheDocument();
  });

  it('edits a server with its current values', async () => {
    const user = userEvent.setup();
    listMock.mockResolvedValue([makeConnection()]);
    vi.mocked(api.updateConnection).mockResolvedValue(makeConnection());
    render(<ServerSidebar />);

    await user.click(await screen.findByRole('button', { name: 'Edit web' }));
    const dialog = screen.getByRole('dialog', { name: 'Edit server' });
    expect(within(dialog).getByLabelText('Host or SSH alias')).toHaveValue('example.org');
    await user.clear(within(dialog).getByLabelText('Host or SSH alias'));
    await user.type(within(dialog).getByLabelText('Host or SSH alias'), 'new.example.org');
    await user.click(within(dialog).getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(api.updateConnection).toHaveBeenCalledTimes(1));
    expect(vi.mocked(api.updateConnection).mock.calls[0]?.[0]).toBe('c1');
    expect(vi.mocked(api.updateConnection).mock.calls[0]?.[1]).toMatchObject({ host: 'new.example.org' });
  });

  it('removes a server only after confirmation', async () => {
    const user = userEvent.setup();
    listMock.mockResolvedValue([makeConnection()]);
    vi.mocked(api.removeConnection).mockResolvedValue(undefined);
    render(<ServerSidebar />);

    await user.click(await screen.findByRole('button', { name: 'Remove web' }));
    expect(api.removeConnection).not.toHaveBeenCalled();
    await user.click(screen.getByRole('button', { name: 'Confirm remove web' }));

    await waitFor(() => expect(api.removeConnection).toHaveBeenCalledWith('c1'));
  });

  it('shows connection state, latency and an actionable failure', async () => {
    const user = userEvent.setup();
    listMock.mockResolvedValue([makeConnection()]);
    const testMock = vi.mocked(api.testConnection);
    testMock.mockResolvedValueOnce({
      state: 'Connected',
      testedAt: '2026-10-25T10:00:00Z',
      message: null,
      detail: null,
      latencyMilliseconds: 42,
      lastConnectedAt: '2026-10-25T10:00:00Z',
    });
    render(<ServerSidebar />);

    await user.click(await screen.findByRole('button', { name: 'Test web' }));
    expect(await screen.findByText('Connected · 42 ms')).toBeInTheDocument();

    testMock.mockResolvedValueOnce({
      state: 'AuthenticationFailed',
      testedAt: '2026-10-25T10:01:00Z',
      message: 'Authentication failed for deploy@example.org:22.',
      detail: null,
      latencyMilliseconds: null,
      lastConnectedAt: null,
    });
    await user.click(screen.getByRole('button', { name: 'Test web' }));
    expect(await screen.findByText('Authentication failed')).toBeInTheDocument();
    expect(screen.getByText(/Authentication failed for deploy/)).toBeInTheDocument();
  });

  it('imports the ssh config and lists unsupported directives', async () => {
    const user = userEvent.setup();
    vi.mocked(api.importSshConfig).mockResolvedValue({
      configPath: '/home/me/.ssh/config',
      created: 2,
      updated: 1,
      issues: [
        {
          filePath: '/home/me/.ssh/config',
          line: 4,
          directive: 'ForwardAgent',
          reason: 'This directive is not supported by the SSH transport and was ignored.',
          affectedAliases: ['web'],
        },
      ],
    });
    render(<ServerSidebar />);

    await user.click(screen.getByRole('button', { name: 'Import ~/.ssh/config' }));

    const report = await screen.findByRole('status');
    expect(report).toHaveTextContent('2 added, 1 updated');
    expect(report).toHaveTextContent('ForwardAgent');
    expect(report).toHaveTextContent('Hosts: web');
    expect(screen.getByText(/3 hosts · imported \d\d:\d\d/)).toBeInTheDocument();
  });

  it('collapses a group', async () => {
    const user = userEvent.setup();
    listMock.mockResolvedValue([makeConnection({ groupName: 'prod' })]);
    render(<ServerSidebar />);

    const toggle = await screen.findByRole('button', { name: 'prod', expanded: true });
    await user.click(toggle);

    expect(screen.getByRole('button', { name: 'prod', expanded: false })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'web', pressed: false })).not.toBeInTheDocument();
  });

  it('describes an unreachable server as offline', async () => {
    const user = userEvent.setup();
    listMock.mockResolvedValue([makeConnection()]);
    vi.mocked(api.testConnection).mockResolvedValueOnce({
      state: 'Unreachable',
      testedAt: '2026-10-25T10:00:00Z',
      message: 'Host is unreachable.',
      detail: null,
      latencyMilliseconds: null,
      lastConnectedAt: null,
    });
    render(<ServerSidebar />);

    await user.click(await screen.findByRole('button', { name: 'Test web' }));

    expect(await screen.findByText('Offline')).toBeInTheDocument();
  });

  it('renames and ungroups a group', async () => {
    const user = userEvent.setup();
    listMock.mockResolvedValue([makeConnection({ groupName: 'prod' })]);
    vi.mocked(api.renameGroup).mockResolvedValue(undefined);
    vi.mocked(api.removeGroup).mockResolvedValue(undefined);
    render(<ServerSidebar />);

    await user.click(await screen.findByRole('button', { name: 'Rename group prod' }));
    const nameInput = screen.getByRole('textbox', { name: 'New name for group prod' });
    await user.clear(nameInput);
    await user.type(nameInput, 'live{Enter}');
    await waitFor(() => expect(api.renameGroup).toHaveBeenCalledWith('prod', 'live'));
    await user.click(screen.getByRole('button', { name: 'Ungroup prod' }));
    await waitFor(() => expect(api.removeGroup).toHaveBeenCalledWith('prod'));
  });

  it('filters servers by name, host, user or group', async () => {
    const user = userEvent.setup();
    listMock.mockResolvedValue([
      makeConnection({ id: 'a', name: 'web-01', groupName: 'prod' }),
      makeConnection({ id: 'b', name: 'nas', host: '192.168.1.20', userName: 'admin', groupName: 'home' }),
    ]);
    render(<ServerSidebar />);
    const filter = await screen.findByRole('searchbox', { name: 'Filter servers' });

    await user.type(filter, 'ADMIN');
    expect(screen.queryByRole('button', { name: 'web-01' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'nas' })).toBeInTheDocument();

    await user.clear(filter);
    await user.type(filter, 'prod');
    expect(screen.getByRole('button', { name: 'web-01' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'nas' })).not.toBeInTheDocument();

    await user.clear(filter);
    await user.type(filter, 'missing');
    expect(screen.getByText('No servers match the filter.')).toBeInTheDocument();

    await user.keyboard('{Escape}');
    expect(filter).toHaveValue('');
  });

  it('focuses the filter with the keyboard shortcut', async () => {
    listMock.mockResolvedValue([makeConnection()]);
    render(<ServerSidebar />);
    const filter = await screen.findByRole('searchbox', { name: 'Filter servers' });

    fireEvent.keyDown(document.body, { key: 'k', metaKey: true });
    expect(filter).toHaveFocus();

    filter.blur();
    fireEvent.keyDown(document.body, { key: 'k', ctrlKey: true });
    expect(filter).toHaveFocus();
  });

  it('shows a compact rail with initials and status while collapsed', async () => {
    restoreWorkspaceLayout([], ['servers']);
    listMock.mockResolvedValue([
      makeConnection({ id: 'a', name: 'web-01', groupName: 'prod' }),
      makeConnection({ id: 'b', name: 'db-01', groupName: 'prod', readOnly: true }),
      makeConnection({ id: 'c', name: 'nas', groupName: 'home' }),
    ]);
    const user = userEvent.setup();
    renderInRegion();

    const rail = await screen.findByRole('navigation', { name: 'Servers, collapsed' });
    const web = within(rail).getByRole('button', { name: 'web-01, not tested' });
    expect(web).toHaveTextContent('w1');
    expect(within(rail).getByRole('button', { name: 'db-01, not tested, read-only' })).toHaveTextContent('d1');
    expect(screen.queryByRole('searchbox', { name: 'Filter servers' })).not.toBeInTheDocument();

    await user.click(web);
    expect(within(rail).getByRole('button', { name: 'web-01, not tested', pressed: true })).toBeInTheDocument();

    await user.click(within(rail).getByRole('button', { name: 'Expand Servers' }));
    expect(screen.getByRole('searchbox', { name: 'Filter servers' })).toBeInTheDocument();
  });

  it('expands the collapsed rail and focuses the filter with the keyboard shortcut', async () => {
    restoreWorkspaceLayout([], ['servers']);
    listMock.mockResolvedValue([makeConnection()]);
    renderInRegion();
    await screen.findByRole('navigation', { name: 'Servers, collapsed' });

    fireEvent.keyDown(document.body, { key: 'k', metaKey: true });

    expect(await screen.findByRole('searchbox', { name: 'Filter servers' })).toHaveFocus();
  });
});

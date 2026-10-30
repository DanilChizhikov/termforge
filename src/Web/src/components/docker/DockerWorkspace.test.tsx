import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { DockerContainer, DockerOverview } from '../../api/docker';
import { replaceConnections } from '../../state/connections';
import { isConfirmationSkipped, reloadConfirmationPreferences } from '../../state/dockerConfirmPreferences';
import { selectServer } from '../../state/selectedServer';
import type { TerminalViewProps } from '../TerminalView';
import { DockerWorkspace } from './DockerWorkspace';

const mocks = vi.hoisted(() => ({
  views: [] as TerminalViewProps[],
  closeTerminal: vi.fn(() => Promise.resolve()),
}));

vi.mock('../TerminalView', () => ({
  TerminalView: function MockTerminalView(props: TerminalViewProps) {
    mocks.views.push(props);
    return <div data-testid="terminal-view" />;
  },
}));

vi.mock('../../api/terminals', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/terminals')>()),
  closeTerminal: mocks.closeTerminal,
}));

class FakeWebSocket {
  static instances: FakeWebSocket[] = [];

  readonly url: string;
  onopen: (() => void) | null = null;
  onmessage: ((event: MessageEvent) => void) | null = null;
  onclose: (() => void) | null = null;
  closed = false;

  constructor(url: string) {
    this.url = url;
    FakeWebSocket.instances.push(this);
  }

  close(): void {
    this.closed = true;
  }

  receive(message: unknown): void {
    this.onmessage?.({ data: JSON.stringify(message) } as MessageEvent);
  }
}

function container(overrides: Partial<DockerContainer>): DockerContainer {
  return {
    id: 'id-web',
    name: 'web',
    image: 'nginx:1.27-alpine',
    state: 'Running',
    status: 'Up 6 days (healthy)',
    health: 'healthy',
    ports: '0.0.0.0:8080->80/tcp',
    createdAt: '2026-10-20 09:12:44 +0000 UTC',
    ...overrides,
  };
}

function overview(overrides: Partial<DockerOverview> = {}): DockerOverview {
  return {
    connectionId: 'srv-1',
    serverName: 'web-01',
    availability: { state: 'Available', reason: 'The Docker daemon is reachable (server version 27.3.1).' },
    containers: [
      container({}),
      container({ id: 'id-db', name: 'db', image: 'postgres:17', state: 'Exited', status: 'Exited (1) 2 hours ago', health: null, ports: '' }),
      container({ id: 'id-cache', name: 'cache', image: 'redis:7', state: 'Restarting', status: 'Restarting (1) 3 seconds ago', health: null, ports: '' }),
    ],
    ...overrides,
  };
}

interface RecordedRequest {
  url: string;
  method: string;
  body: unknown;
}

function stubApi(current: () => DockerOverview, actionResponse: () => Response = () => json({})) {
  const requests: RecordedRequest[] = [];
  const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = init?.method ?? 'GET';
    const body = typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined;
    requests.push({ url, method, body });
    if (url === '/api/session') {
      return Promise.resolve(json({ csrfToken: 'token' }));
    }
    if (method === 'GET') {
      return Promise.resolve(json(current()));
    }
    if (url === '/api/confirmations') {
      return Promise.resolve(json({ token: 'confirm-1', expiresAt: '', operation: 'ContainerRestart', serverName: 'web-01', target: 'web' }));
    }
    if (url.endsWith('/shell')) {
      return Promise.resolve(json({ id: 'shell-1', connectionId: 'srv-1', connectionName: 'web-01', sessionNumber: 1, openedAt: '' }));
    }
    return Promise.resolve(actionResponse());
  });
  vi.stubGlobal('fetch', fetchMock);
  return requests;
}

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status });
}

function actionRequests(requests: RecordedRequest[]): RecordedRequest[] {
  return requests.filter((request) => request.method === 'POST' && request.url.includes('/containers/'));
}

beforeEach(() => {
  FakeWebSocket.instances = [];
  mocks.views.length = 0;
  mocks.closeTerminal.mockClear();
  vi.stubGlobal('WebSocket', FakeWebSocket);
});

afterEach(() => {
  vi.unstubAllGlobals();
  act(() => selectServer(null));
});

describe('DockerWorkspace', () => {
  it('asks to select a server first and loads nothing', () => {
    const requests = stubApi(() => overview());

    render(<DockerWorkspace active />);

    expect(screen.getByText('Select a server to manage Docker containers.')).toBeInTheDocument();
    expect(requests).toHaveLength(0);
  });

  it('loads nothing until the tab is activated', () => {
    act(() => selectServer('srv-1'));
    const requests = stubApi(() => overview());

    render(<DockerWorkspace active={false} />);

    expect(requests).toHaveLength(0);
  });

  it('lists containers with status shown as color, shape and word', async () => {
    act(() => selectServer('srv-1'));
    const requests = stubApi(() => overview());

    render(<DockerWorkspace active />);

    expect(await screen.findByText('Docker reachable')).toBeInTheDocument();
    expect(requests[0]?.url).toBe('/api/connections/srv-1/docker');
    expect(screen.getByRole('heading', { name: 'Containers on web-01' })).toBeInTheDocument();
    const rows = screen.getAllByRole('row');
    expect(within(rows[1]!).getByText('running · healthy')).toBeInTheDocument();
    expect(within(rows[2]!).getByText('exited (1) · 2 h ago')).toBeInTheDocument();
    expect(within(rows[1]!).getByRole('button', { name: 'Shell in web' })).toBeInTheDocument();
    expect(within(rows[2]!).queryByRole('button', { name: /Shell/ })).not.toBeInTheDocument();
    expect(within(rows[2]!).getByRole('button', { name: 'Start db' })).toBeInTheDocument();
  });

  it('filters running and problem containers', async () => {
    const user = userEvent.setup();
    act(() => selectServer('srv-1'));
    stubApi(() => overview());
    render(<DockerWorkspace active />);
    await screen.findByText('Docker reachable');

    await user.click(screen.getByRole('button', { name: 'Running 1' }));
    expect(screen.getAllByRole('row')).toHaveLength(2);

    await user.click(screen.getByRole('button', { name: 'Problems 2' }));
    const names = screen.getAllByRole('row').slice(1).map((row) => row.querySelector('.docker-name')?.textContent);
    expect(names).toEqual(['db', 'cache']);
  });

  it('explains why Docker is missing without listing containers', async () => {
    act(() => selectServer('srv-1'));
    stubApi(() =>
      overview({
        availability: { state: 'NotInstalled', reason: 'Docker is not installed on the server.' },
        containers: [],
      }),
    );

    render(<DockerWorkspace active />);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Docker is not installed');
    expect(alert).toHaveTextContent('Docker is not installed on the server.');
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('explains a permission problem with the docker group hint', async () => {
    act(() => selectServer('srv-1'));
    stubApi(() =>
      overview({
        availability: {
          state: 'NoAccess',
          reason: 'Permission denied: the user is not in the docker group. Add the user to the docker group (then log in again) or use rootless Docker.',
        },
        containers: [],
      }),
    );

    render(<DockerWorkspace active />);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Docker is not accessible');
    expect(alert).toHaveTextContent('not in the docker group');
  });

  it('asks before stopping and does nothing when cancelled', async () => {
    const user = userEvent.setup();
    act(() => selectServer('srv-1'));
    const requests = stubApi(() => overview());
    render(<DockerWorkspace active />);
    await screen.findByText('Docker reachable');

    await user.click(screen.getByRole('button', { name: 'Stop web' }));
    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveTextContent('Stop web on web-01?');
    expect(within(dialog).getByRole('button', { name: 'Cancel' })).toHaveFocus();
    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(actionRequests(requests)).toHaveLength(0);
  });

  it('restarts after confirmation, reports it and refreshes the list', async () => {
    const user = userEvent.setup();
    act(() => selectServer('srv-1'));
    const requests = stubApi(() => overview(), () => json({ operationId: 'op-1', container: 'web', action: 'restart' }));
    render(<DockerWorkspace active />);
    await screen.findByText('Docker reachable');

    await user.click(screen.getByRole('button', { name: 'Restart web' }));
    await user.click(screen.getByRole('button', { name: 'Restart container' }));

    expect(await screen.findByText('Restarted web. Recorded in the journal.')).toBeInTheDocument();
    const action = actionRequests(requests)[0]!;
    expect(action.url).toBe('/api/connections/srv-1/docker/containers/web/restart');
    expect(action.body).toEqual({ confirmationToken: 'confirm-1' });
    const confirmation = requests.find((request) => request.url === '/api/confirmations');
    expect(confirmation?.body).toEqual({ operation: 'ContainerRestart', connectionId: 'srv-1', target: 'web' });
    expect(requests.filter((request) => request.method === 'GET' && request.url.endsWith('/docker'))).toHaveLength(2);
  });

  it('blocks container actions and shells on a read-only server', async () => {
    act(() =>
      replaceConnections([
        {
          id: 'srv-1',
          name: 'web-01',
          groupName: null,
          host: 'web.example.test',
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
          readOnly: true,
          aiAccess: false,
        },
      ]),
    );
    act(() => selectServer('srv-1'));
    stubApi(() => overview());
    render(<DockerWorkspace active />);
    await screen.findByText('Docker reachable');

    expect(screen.getByText(/web-01 is read-only/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Stop web' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Start db' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Shell in web' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Logs of web' })).toBeEnabled();
  });

  it('starts a stopped container without a confirmation dialog', async () => {
    const user = userEvent.setup();
    act(() => selectServer('srv-1'));
    const requests = stubApi(() => overview(), () => json({ operationId: 'op-2', container: 'db', action: 'start' }));
    render(<DockerWorkspace active />);
    await screen.findByText('Docker reachable');

    await user.click(screen.getByRole('button', { name: 'Start db' }));

    expect(await screen.findByText('Started db. Recorded in the journal.')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(actionRequests(requests)[0]?.body).toEqual({ confirmationToken: null });
    expect(requests.some((request) => request.url === '/api/confirmations')).toBe(false);
  });

  it('shows the server reason when an action fails', async () => {
    const user = userEvent.setup();
    act(() => selectServer('srv-1'));
    stubApi(
      () => overview(),
      () => json({ detail: 'The container does not exist on the server. Refresh the container list.' }, 404),
    );
    render(<DockerWorkspace active />);
    await screen.findByText('Docker reachable');

    await user.click(screen.getByRole('button', { name: 'Start db' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('The container does not exist on the server.');
  });

  it('streams logs with tail and follow, filters lines and reconnects on option change', async () => {
    const user = userEvent.setup();
    act(() => selectServer('srv-1'));
    stubApi(() => overview());
    render(<DockerWorkspace active />);
    await screen.findByText('Docker reachable');

    await user.click(screen.getByRole('button', { name: 'Logs of web' }));
    const socket = FakeWebSocket.instances[0]!;
    expect(socket.url).toBe(
      `ws://${window.location.host}/api/connections/srv-1/docker/containers/web/logs/ws?tail=200&follow=true`,
    );
    act(() => socket.onopen?.());
    act(() => socket.receive({ type: 'output', text: 'INFO started\nWARN slow request\n' }));

    const output = screen.getByLabelText('Log output');
    expect(output).toHaveTextContent('INFO started');
    await user.type(screen.getByRole('searchbox', { name: 'Filter log lines' }), 'warn');
    expect(output).not.toHaveTextContent('INFO started');
    expect(output).toHaveTextContent('WARN slow request');

    await user.click(screen.getByRole('checkbox', { name: 'Follow' }));
    expect(socket.closed).toBe(true);
    const reopened = FakeWebSocket.instances[1]!;
    expect(reopened.url).toContain('tail=200&follow=false');
    act(() => reopened.receive({ type: 'end', exitCode: 0 }));
    expect(screen.getByText('Stream ended (exit code 0)')).toBeInTheDocument();
  });

  it('reports a log stream error from the server', async () => {
    const user = userEvent.setup();
    act(() => selectServer('srv-1'));
    stubApi(() => overview());
    render(<DockerWorkspace active />);
    await screen.findByText('Docker reachable');

    await user.click(screen.getByRole('button', { name: 'Logs of db' }));
    act(() => FakeWebSocket.instances[0]!.receive({ type: 'error', message: 'Permission denied: the user is not in the docker group.' }));

    expect(screen.getByRole('alert')).toHaveTextContent('not in the docker group');
  });

  it('opens a shell through the docker endpoint and closes the session with the panel', async () => {
    const user = userEvent.setup();
    act(() => selectServer('srv-1'));
    const requests = stubApi(() => overview());
    render(<DockerWorkspace active />);
    await screen.findByText('Docker reachable');

    await user.click(screen.getByRole('button', { name: 'Shell in web' }));
    const view = mocks.views.at(-1)!;
    expect(view.connectionId).toBe('srv-1');
    const session = await view.open!({ columns: 100, rows: 30 });
    act(() => view.onOpened(session));
    act(() => view.onStateChange('connected', null));

    const shell = requests.find((request) => request.url.endsWith('/shell'))!;
    expect(shell.url).toBe('/api/connections/srv-1/docker/containers/web/shell');
    expect(shell.body).toEqual({ columns: 100, rows: 30, shell: 'auto' });
    expect(screen.getByRole('region', { name: 'Shell in web' })).toHaveTextContent('Connected');

    await user.click(screen.getByRole('button', { name: 'Close shell' }));
    expect(mocks.closeTerminal).toHaveBeenCalledWith('shell-1');
  });
});

describe('DockerWorkspace removal and compose', () => {
  afterEach(() => {
    window.localStorage.clear();
    act(() => reloadConfirmationPreferences());
  });

  it('removes a running container after a force-stop warning and remembers "Don\'t ask again"', async () => {
    const user = userEvent.setup();
    act(() => selectServer('srv-1'));
    const requests = stubApi(() => overview(), () => json({ operationId: 'op-3', container: 'web', action: 'remove' }));
    render(<DockerWorkspace active />);
    await screen.findByText('Docker reachable');

    await user.click(screen.getByRole('button', { name: 'Remove web' }));
    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveTextContent('Remove web on web-01?');
    expect(dialog).toHaveTextContent('force-stopped');
    await user.click(within(dialog).getByRole('checkbox', { name: "Don't ask again" }));
    await user.click(within(dialog).getByRole('button', { name: 'Remove container' }));

    expect(await screen.findByText('Removed web. Recorded in the journal.')).toBeInTheDocument();
    expect(isConfirmationSkipped('containerRemove')).toBe(true);
    const action = actionRequests(requests)[0]!;
    expect(action.url).toBe('/api/connections/srv-1/docker/containers/web/remove');
    expect(action.body).toEqual({ confirmationToken: 'confirm-1' });
    const confirmation = requests.find((request) => request.url === '/api/confirmations');
    expect(confirmation?.body).toEqual({ operation: 'ContainerRemove', connectionId: 'srv-1', target: 'web' });

    await user.click(screen.getByRole('button', { name: 'Remove db' }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    await screen.findByText('Removed db. Recorded in the journal.');
    expect(actionRequests(requests).map((request) => request.url).at(-1)).toBe('/api/connections/srv-1/docker/containers/db/remove');
    expect(requests.filter((request) => request.url === '/api/confirmations')).toHaveLength(2);
  });

  it('groups compose containers and restarts the whole project after confirmation', async () => {
    const user = userEvent.setup();
    act(() => selectServer('srv-1'));
    const shop = { project: 'shop', workingDirectory: '/srv/shop' };
    const requests = stubApi(
      () =>
        overview({
          containers: [
            container({ id: 'id-shop-web', name: 'shop-web', compose: shop }),
            container({ id: 'id-shop-db', name: 'shop-db', compose: shop }),
            container({ id: 'id-solo', name: 'solo' }),
          ],
        }),
      () => json({ operationId: 'op-4', project: 'shop', action: 'restart' }),
    );
    render(<DockerWorkspace active />);
    await screen.findByText('Docker reachable');

    expect(screen.getByText('/srv/shop')).toBeInTheDocument();
    expect(screen.getByText('Standalone containers')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Restart project shop' }));
    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveTextContent('Restart project shop on web-01?');
    expect(within(dialog).queryByRole('checkbox')).not.toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Restart project' }));

    expect(await screen.findByText('Restarted project shop. Recorded in the journal.')).toBeInTheDocument();
    const action = requests.find((request) => request.method === 'POST' && request.url.includes('/compose/'))!;
    expect(action.url).toBe('/api/connections/srv-1/docker/compose/shop/restart');
    expect(action.body).toEqual({ confirmationToken: 'confirm-1' });
  });
});

describe('DockerWorkspace multi-select', () => {
  afterEach(() => {
    window.localStorage.clear();
    act(() => reloadConfirmationPreferences());
  });

  it('stops every selected running container after one confirmation', async () => {
    const user = userEvent.setup();
    act(() => selectServer('srv-1'));
    const requests = stubApi(() => overview(), () => json({ operationId: 'op-5', container: 'web', action: 'stop' }));
    render(<DockerWorkspace active />);
    await screen.findByText('Docker reachable');

    await user.click(screen.getByRole('checkbox', { name: 'Select web' }));
    await user.click(screen.getByRole('checkbox', { name: 'Select cache' }));
    expect(screen.getByText('2 selected')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Start .*selected/ })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Stop 2 selected containers' }));
    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveTextContent('Stop 2 containers on web-01?');
    expect(dialog).toHaveTextContent('web, cache.');
    await user.click(within(dialog).getByRole('button', { name: 'Stop 2 containers' }));

    expect(await screen.findByText('Stopped 2 containers. Recorded in the journal.')).toBeInTheDocument();
    expect(actionRequests(requests).map((request) => request.url)).toEqual([
      '/api/connections/srv-1/docker/containers/web/stop',
      '/api/connections/srv-1/docker/containers/cache/stop',
    ]);
    expect(screen.queryByText('2 selected')).not.toBeInTheDocument();
  });

  it('starts only the stopped containers of a selection without a dialog', async () => {
    const user = userEvent.setup();
    act(() => selectServer('srv-1'));
    const requests = stubApi(() => overview(), () => json({ operationId: 'op-6', container: 'db', action: 'start' }));
    render(<DockerWorkspace active />);
    await screen.findByText('Docker reachable');

    await user.click(screen.getByRole('checkbox', { name: 'Select all containers' }));
    expect(screen.getByText('3 selected')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Start 1 selected container' }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(await screen.findByText('Started 1 container. Recorded in the journal.')).toBeInTheDocument();
    expect(actionRequests(requests).map((request) => request.url)).toEqual(['/api/connections/srv-1/docker/containers/db/start']);
  });

  it('reports which selected containers failed and keeps going', async () => {
    const user = userEvent.setup();
    act(() => selectServer('srv-1'));
    let call = 0;
    const requests = stubApi(
      () => overview(),
      () => {
        call += 1;
        return call === 1 ? json({ detail: 'Container web is busy.' }, 409) : json({ operationId: 'op-7', container: 'cache', action: 'restart' });
      },
    );
    render(<DockerWorkspace active />);
    await screen.findByText('Docker reachable');

    await user.click(screen.getByRole('checkbox', { name: 'Select web' }));
    await user.click(screen.getByRole('checkbox', { name: 'Select cache' }));
    await user.click(screen.getByRole('button', { name: 'Restart 2 selected containers' }));
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Restart 2 containers' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Restarted 1 of 2 containers. Failed: web: Container web is busy.');
    expect(actionRequests(requests)).toHaveLength(2);
  });

  it('cannot select containers on a read-only server', async () => {
    act(() =>
      replaceConnections([
        {
          id: 'srv-1',
          name: 'web-01',
          groupName: null,
          host: 'web.example.test',
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
          readOnly: true,
          aiAccess: false,
        },
      ]),
    );
    act(() => selectServer('srv-1'));
    stubApi(() => overview());
    render(<DockerWorkspace active />);
    await screen.findByText('Docker reachable');

    expect(screen.getByRole('checkbox', { name: 'Select all containers' })).toBeDisabled();
    expect(screen.getByRole('checkbox', { name: 'Select web' })).toBeDisabled();
    act(() => replaceConnections([]));
  });
});

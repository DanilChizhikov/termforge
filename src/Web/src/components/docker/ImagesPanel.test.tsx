import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { DockerImage, DockerOverview } from '../../api/docker';
import { reloadConfirmationPreferences, setConfirmationSkipped } from '../../state/dockerConfirmPreferences';
import { selectServer } from '../../state/selectedServer';
import { DockerWorkspace } from './DockerWorkspace';

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

interface RecordedRequest {
  url: string;
  method: string;
  body: unknown;
}

const OVERVIEW: DockerOverview = {
  connectionId: 'srv-1',
  serverName: 'web-01',
  availability: { state: 'Available', reason: 'reachable' },
  containers: [
    {
      id: 'id-web',
      name: 'web',
      image: 'nginx:1.27',
      state: 'Running',
      status: 'Up 1 hour',
      health: null,
      ports: '',
      createdAt: '',
      compose: null,
    },
  ],
};

const IMAGES: DockerImage[] = [
  { id: 'sha256:aaaaaaaaaaaaaaaa', repository: 'nginx', tag: '1.27', size: '192MB', createdSince: '2 weeks ago' },
  { id: 'sha256:bbbbbbbbbbbbbbbb', repository: '<none>', tag: '<none>', size: '80MB', createdSince: '3 months ago' },
];

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status });
}

function stubApi(removeResponse: () => Response = () => json({ operationId: 'op-1', image: 'x' })): RecordedRequest[] {
  const requests: RecordedRequest[] = [];
  vi.stubGlobal(
    'fetch',
    vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const method = init?.method ?? 'GET';
      const body = typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined;
      requests.push({ url, method, body });
      if (url === '/api/session') {
        return Promise.resolve(json({ csrfToken: 'token' }));
      }
      if (url.endsWith('/docker/images')) {
        return Promise.resolve(json({ connectionId: 'srv-1', serverName: 'web-01', images: IMAGES }));
      }
      if (method === 'GET') {
        return Promise.resolve(json(OVERVIEW));
      }
      if (url === '/api/confirmations') {
        return Promise.resolve(json({ token: 'confirm-1', expiresAt: '', operation: 'ImageRemove', serverName: 'web-01', target: '' }));
      }
      return Promise.resolve(removeResponse());
    }),
  );
  return requests;
}

async function openImages(): Promise<void> {
  const user = userEvent.setup();
  render(<DockerWorkspace active />);
  await screen.findByText('Docker reachable');
  await user.click(screen.getByRole('button', { name: 'Images' }));
  await screen.findByText('192MB');
}

function removals(requests: RecordedRequest[]): RecordedRequest[] {
  return requests.filter((request) => request.url.endsWith('/images/remove'));
}

beforeEach(() => {
  FakeWebSocket.instances = [];
  vi.stubGlobal('WebSocket', FakeWebSocket);
  act(() => selectServer('srv-1'));
});

afterEach(() => {
  vi.unstubAllGlobals();
  act(() => selectServer(null));
  window.localStorage.clear();
  act(() => reloadConfirmationPreferences());
});

describe('Docker images', () => {
  it('lists images with short ids and the containers using them', async () => {
    stubApi();
    await openImages();

    expect(screen.getByRole('heading', { name: 'Images on web-01' })).toBeInTheDocument();
    const rows = screen.getAllByRole('row');
    expect(within(rows[1]!).getByText('aaaaaaaaaaaa')).toBeInTheDocument();
    expect(within(rows[1]!).getByText('1 container')).toBeInTheDocument();
    expect(within(rows[2]!).getByRole('button', { name: 'Remove image sha256:bbbbbbbbbbbbbbbb' })).toBeInTheDocument();
  });

  it('asks before removing an image and does nothing when cancelled', async () => {
    const user = userEvent.setup();
    const requests = stubApi();
    await openImages();

    await user.click(screen.getByRole('button', { name: 'Remove image nginx:1.27' }));
    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveTextContent('Remove image nginx:1.27 on web-01?');
    expect(within(dialog).getByRole('button', { name: 'Cancel' })).toHaveFocus();
    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }));

    expect(removals(requests)).toHaveLength(0);
  });

  it('removes an image after confirmation and skips the dialog once it is turned off', async () => {
    const user = userEvent.setup();
    const requests = stubApi();
    await openImages();

    await user.click(screen.getByRole('button', { name: 'Remove image nginx:1.27' }));
    await user.click(screen.getByRole('button', { name: 'Remove image' }));
    expect(await screen.findByText('Removed nginx:1.27. Recorded in the journal.')).toBeInTheDocument();
    expect(removals(requests)[0]?.body).toEqual({ image: 'nginx:1.27', confirmationToken: 'confirm-1' });

    act(() => setConfirmationSkipped('imageRemove', true));
    await user.click(screen.getByRole('button', { name: 'Remove image sha256:bbbbbbbbbbbbbbbb' }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    await screen.findByText('Removed sha256:bbbbbbbbbbbbbbbb. Recorded in the journal.');
    expect(requests.filter((request) => request.url === '/api/confirmations')).toHaveLength(2);
  });

  it('shows why an image cannot be removed', async () => {
    const user = userEvent.setup();
    stubApi(() => json({ detail: 'The image is used by a container. Remove the container first.' }, 409));
    await openImages();

    await user.click(screen.getByRole('button', { name: 'Remove image nginx:1.27' }));
    await user.click(screen.getByRole('button', { name: 'Remove image' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('The image is used by a container.');
  });

  it('streams pull progress and reloads images when the pull succeeds', async () => {
    const user = userEvent.setup();
    const requests = stubApi();
    await openImages();

    await user.type(screen.getByRole('textbox', { name: 'Image reference' }), 'redis:7');
    await user.click(screen.getByRole('button', { name: 'Pull' }));
    const socket = FakeWebSocket.instances[0]!;
    expect(socket.url).toBe(`ws://${window.location.host}/api/connections/srv-1/docker/images/pull/ws?image=redis%3A7`);
    expect(screen.getByRole('button', { name: 'Pulling…' })).toBeDisabled();

    act(() => socket.onopen?.());
    act(() => socket.receive({ type: 'output', text: '7: Pulling from library/redis\n' }));
    expect(screen.getByLabelText('Pull output')).toHaveTextContent('Pulling from library/redis');
    const imageLoads = requests.filter((request) => request.url.endsWith('/docker/images')).length;
    act(() => socket.receive({ type: 'end', exitCode: 0 }));

    expect(screen.getByText('Pulled. Recorded in the journal.')).toBeInTheDocument();
    await vi.waitFor(() =>
      expect(requests.filter((request) => request.url.endsWith('/docker/images')).length).toBeGreaterThan(imageLoads),
    );
    expect(screen.getByRole('textbox', { name: 'Image reference' })).toHaveValue('');
  });

  it('reports a failed pull', async () => {
    const user = userEvent.setup();
    stubApi();
    await openImages();

    await user.type(screen.getByRole('textbox', { name: 'Image reference' }), 'missing/image:1');
    await user.click(screen.getByRole('button', { name: 'Pull' }));
    act(() => FakeWebSocket.instances[0]!.receive({ type: 'end', exitCode: 1 }));

    expect(screen.getByRole('alert')).toHaveTextContent('Pull failed (exit code 1)');
    expect(screen.getByRole('button', { name: 'Pull' })).toBeEnabled();
  });
});

describe('Docker images multi-select', () => {
  it('removes every selected image after one confirmation', async () => {
    const user = userEvent.setup();
    const requests = stubApi();
    await openImages();

    await user.click(screen.getByRole('checkbox', { name: 'Select all images' }));
    await user.click(screen.getByRole('button', { name: 'Remove 2 selected images' }));
    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveTextContent('Remove 2 images on web-01?');
    expect(dialog).toHaveTextContent('Containers still use some of them');
    await user.click(within(dialog).getByRole('button', { name: 'Remove 2 images' }));

    expect(await screen.findByText('Removed 2 images. Recorded in the journal.')).toBeInTheDocument();
    expect(removals(requests).map((request) => (request.body as { image: string }).image)).toEqual([
      'nginx:1.27',
      'sha256:bbbbbbbbbbbbbbbb',
    ]);
  });

  it('removes selected images without a dialog once it is turned off', async () => {
    const user = userEvent.setup();
    const requests = stubApi();
    await openImages();
    act(() => setConfirmationSkipped('imageRemove', true));

    await user.click(screen.getByRole('checkbox', { name: 'Select image sha256:bbbbbbbbbbbbbbbb' }));
    await user.click(screen.getByRole('button', { name: 'Remove 1 selected image' }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    await screen.findByText('Removed 1 image. Recorded in the journal.');
    expect(removals(requests)).toHaveLength(1);
  });
});

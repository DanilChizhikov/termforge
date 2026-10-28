import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from './client';
import { containerLogsUrl, imagePullUrl, parseLogMessage, removeDockerImage, runComposeAction } from './docker';

interface RecordedCall {
  url: string;
  method: string;
  body: unknown;
}

function recordingClient(calls: RecordedCall[]): ApiClient {
  const fetchImpl = vi.fn<typeof fetch>((input, init) => {
    const url = String(input);
    const body = typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined;
    calls.push({ url, method: init?.method ?? 'GET', body });
    const payload = url === '/api/session' ? { csrfToken: 'csrf' } : { token: 'confirm-1' };
    return Promise.resolve(new Response(JSON.stringify(payload), { status: 200 }));
  });
  return new ApiClient({ fetchImpl });
}

const LOCATION = { protocol: 'http:', host: '127.0.0.1:7420' };

describe('docker api', () => {
  it('builds an encoded log stream url with tail and follow', () => {
    const url = containerLogsUrl('srv 1', 'web', { tail: 200, follow: true }, LOCATION);

    expect(url).toBe('ws://127.0.0.1:7420/api/connections/srv%201/docker/containers/web/logs/ws?tail=200&follow=true');
  });

  it('encodes container names so they cannot change the path', () => {
    const url = containerLogsUrl('srv', '../x?y', { tail: 100, follow: false }, LOCATION);

    expect(url).toContain('/containers/..%2Fx%3Fy/logs/ws?tail=100&follow=false');
  });

  it('parses output, end and error messages', () => {
    expect(parseLogMessage('{"type":"output","text":"hi\\n"}')).toEqual({ type: 'output', text: 'hi\n' });
    expect(parseLogMessage('{"type":"end","exitCode":0}')).toEqual({ type: 'end', exitCode: 0 });
    expect(parseLogMessage('{"type":"end"}')).toEqual({ type: 'end', exitCode: null });
    expect(parseLogMessage('{"type":"error","message":"denied"}')).toEqual({ type: 'error', message: 'denied' });
  });

  it('ignores malformed messages', () => {
    expect(parseLogMessage('not json')).toBeNull();
    expect(parseLogMessage('{"type":"output"}')).toBeNull();
    expect(parseLogMessage('null')).toBeNull();
  });

  it('builds an encoded image pull url', () => {
    const url = imagePullUrl('srv', 'ghcr.io/acme/app:1.2', LOCATION);

    expect(url).toBe('ws://127.0.0.1:7420/api/connections/srv/docker/images/pull/ws?image=ghcr.io%2Facme%2Fapp%3A1.2');
  });

  it('confirms an image removal and sends the image in the body', async () => {
    const calls: RecordedCall[] = [];
    const client = recordingClient(calls);

    await removeDockerImage('srv-1', 'nginx:1.27', client);

    const confirmation = calls.find((call) => call.url === '/api/confirmations');
    expect(confirmation?.body).toEqual({ operation: 'ImageRemove', connectionId: 'srv-1', target: 'nginx:1.27' });
    const removal = calls.at(-1)!;
    expect(removal.url).toBe('/api/connections/srv-1/docker/images/remove');
    expect(removal.body).toEqual({ image: 'nginx:1.27', confirmationToken: 'confirm-1' });
  });

  it('confirms a compose action and encodes the project in the path', async () => {
    const calls: RecordedCall[] = [];
    const client = recordingClient(calls);

    await runComposeAction('srv-1', 'shop', 'recreate', client);

    const confirmation = calls.find((call) => call.url === '/api/confirmations');
    expect(confirmation?.body).toEqual({ operation: 'ComposeRecreate', connectionId: 'srv-1', target: 'shop' });
    const action = calls.at(-1)!;
    expect(action.url).toBe('/api/connections/srv-1/docker/compose/shop/recreate');
    expect(action.body).toEqual({ confirmationToken: 'confirm-1' });
  });
});

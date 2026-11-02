import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiClient, ApiError } from './client';
import { clearOriginProof, getOriginProof, saveOriginProof } from './originProof';

type FetchMock = ReturnType<typeof vi.fn<typeof fetch>>;

function jsonResponse(status: number, body?: unknown): Response {
  const text = body === undefined ? '' : JSON.stringify(body);
  return new Response(text.length > 0 ? text : null, { status });
}

function headerOf(mock: FetchMock, callIndex: number, name: string): string | undefined {
  const init = mock.mock.calls[callIndex]?.[1];
  const headers = init?.headers as Record<string, string> | undefined;
  return headers?.[name];
}

function urlOf(mock: FetchMock, callIndex: number): string {
  return String(mock.mock.calls[callIndex]?.[0]);
}

function createClient(mock: FetchMock, onSessionMissing?: () => void): ApiClient {
  return onSessionMissing === undefined
    ? new ApiClient({ fetchImpl: mock })
    : new ApiClient({ fetchImpl: mock, onSessionMissing });
}

describe('ApiClient', () => {
  afterEach(clearOriginProof);

  it('sends GET requests with same-origin credentials and the origin proof', async () => {
    saveOriginProof('get-proof');
    const mock: FetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(200, { ok: true }));
    const client = createClient(mock);

    const result = await client.get<{ ok: boolean }>('/api/servers');

    expect(result).toEqual({ ok: true });
    expect(mock).toHaveBeenCalledTimes(1);
    expect(mock.mock.calls[0]?.[1]?.credentials).toBe('same-origin');
    expect(headerOf(mock, 0, 'X-CSRF-Token')).toBe('get-proof');
  });

  it.each(['POST', 'PUT', 'PATCH', 'DELETE'] as const)(
    'sends the stored origin proof on %s without cookie-only refresh',
    async (method) => {
      const mock: FetchMock = vi
        .fn<typeof fetch>()
        .mockResolvedValueOnce(jsonResponse(204));
      saveOriginProof('token-1');
      const client = createClient(mock);

      await client.request(method, '/api/things', { a: 1 });

      expect(urlOf(mock, 0)).toBe('/api/things');
      expect(mock).toHaveBeenCalledTimes(1);
      expect(mock.mock.calls[0]?.[1]?.method).toBe(method);
      expect(headerOf(mock, 0, 'X-CSRF-Token')).toBe('token-1');
      expect(mock.mock.calls[0]?.[1]?.credentials).toBe('same-origin');
    },
  );

  it('reads the proof saved by a new login on every request', async () => {
    const mock: FetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValue(jsonResponse(204));
    saveOriginProof('token-1');
    const client = createClient(mock);

    await client.post('/api/a');
    saveOriginProof('token-2');
    await client.post('/api/b');

    const sessionCalls = mock.mock.calls.filter((call) => String(call[0]) === '/api/session');
    expect(sessionCalls).toHaveLength(0);
    expect(headerOf(mock, 0, 'X-CSRF-Token')).toBe('token-1');
    expect(headerOf(mock, 1, 'X-CSRF-Token')).toBe('token-2');
  });

  it('uses one stored proof for concurrent requests without fetching a session', async () => {
    const mock: FetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValue(jsonResponse(204));
    saveOriginProof('token-1');
    const client = createClient(mock);

    await Promise.all([client.post('/api/a'), client.post('/api/b')]);

    const sessionCalls = mock.mock.calls.filter((call) => String(call[0]) === '/api/session');
    expect(sessionCalls).toHaveLength(0);
    expect(headerOf(mock, 0, 'X-CSRF-Token')).toBe('token-1');
    expect(headerOf(mock, 1, 'X-CSRF-Token')).toBe('token-1');
  });

  it('does not refresh or retry a rejected proof and requests a new login', async () => {
    const mock: FetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(new Response('A valid CSRF token is required', { status: 403 }));
    const onSessionMissing = vi.fn();
    const client = createClient(mock, onSessionMissing);
    saveOriginProof('old');

    await expect(client.post('/api/second')).rejects.toMatchObject({ status: 403, isSessionMissing: true });

    expect(headerOf(mock, 0, 'X-CSRF-Token')).toBe('old');
    expect(mock).toHaveBeenCalledTimes(1);
    expect(onSessionMissing).toHaveBeenCalledOnce();
  });

  it('does not treat an application permission failure as a missing session', async () => {
    const mock: FetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(jsonResponse(403, { error: 'forbidden' }));
    const onSessionMissing = vi.fn();
    const client = createClient(mock, onSessionMissing);
    saveOriginProof('fresh');

    await expect(client.post('/api/forbidden')).rejects.toMatchObject({ status: 403 });
    expect(mock).toHaveBeenCalledTimes(1);
    expect(onSessionMissing).not.toHaveBeenCalled();
  });

  it('reports a missing session on 401 and throws ApiError', async () => {
    const mock: FetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(401));
    const onSessionMissing = vi.fn();
    const client = createClient(mock, onSessionMissing);
    saveOriginProof('expired');

    const failure = client.get('/api/servers');

    await expect(failure).rejects.toBeInstanceOf(ApiError);
    expect(onSessionMissing).toHaveBeenCalledTimes(1);
    expect(getOriginProof()).toBeNull();
  });

  it('serializes bodies as JSON', async () => {
    const mock: FetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(jsonResponse(204));
    const client = createClient(mock);

    await client.post('/api/things', { name: 'x' });

    expect(mock.mock.calls[0]?.[1]?.body).toBe('{"name":"x"}');
    expect(headerOf(mock, 0, 'Content-Type')).toBe('application/json');
  });

  it('does not obtain a proof from a cookie-only session response', async () => {
    clearOriginProof();
    const mock: FetchMock = vi.fn<typeof fetch>().mockImplementation(async () => jsonResponse(200, { csrfToken: 'leaked' }));
    const client = createClient(mock);

    await client.getSession();
    await client.get('/api/servers');

    expect(getOriginProof()).toBeNull();
    expect(headerOf(mock, 0, 'X-CSRF-Token')).toBeUndefined();
    expect(headerOf(mock, 1, 'X-CSRF-Token')).toBeUndefined();
  });

  it('sends the origin proof on streaming requests', async () => {
    saveOriginProof('stream-proof');
    const mock: FetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(200, {}));
    const client = createClient(mock);
    const controller = new AbortController();

    await client.openStream('/api/recordings/id/cast', controller.signal);

    expect(headerOf(mock, 0, 'X-CSRF-Token')).toBe('stream-proof');
  });

  it('rejects paths outside /api', async () => {
    const mock: FetchMock = vi.fn<typeof fetch>();
    const client = createClient(mock);

    await expect(client.get('/other')).rejects.toThrow('/api');
    expect(mock).not.toHaveBeenCalled();
  });
});

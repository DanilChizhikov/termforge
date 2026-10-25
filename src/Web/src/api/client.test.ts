import { describe, expect, it, vi } from 'vitest';
import { ApiClient, ApiError } from './client';

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
  it('sends GET requests with same-origin credentials and no CSRF header', async () => {
    const mock: FetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(200, { ok: true }));
    const client = createClient(mock);

    const result = await client.get<{ ok: boolean }>('/api/servers');

    expect(result).toEqual({ ok: true });
    expect(mock).toHaveBeenCalledTimes(1);
    expect(mock.mock.calls[0]?.[1]?.credentials).toBe('same-origin');
    expect(headerOf(mock, 0, 'X-CSRF-Token')).toBeUndefined();
  });

  it.each(['POST', 'PUT', 'PATCH', 'DELETE'] as const)(
    'fetches the CSRF token and sends it on %s',
    async (method) => {
      const mock: FetchMock = vi
        .fn<typeof fetch>()
        .mockResolvedValueOnce(jsonResponse(200, { csrfToken: 'token-1' }))
        .mockResolvedValueOnce(jsonResponse(204));
      const client = createClient(mock);

      await client.request(method, '/api/things', { a: 1 });

      expect(urlOf(mock, 0)).toBe('/api/session');
      expect(urlOf(mock, 1)).toBe('/api/things');
      expect(mock.mock.calls[1]?.[1]?.method).toBe(method);
      expect(headerOf(mock, 1, 'X-CSRF-Token')).toBe('token-1');
      expect(mock.mock.calls[1]?.[1]?.credentials).toBe('same-origin');
    },
  );

  it('caches the CSRF token across unsafe requests', async () => {
    const mock: FetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(jsonResponse(200, { csrfToken: 'token-1' }))
      .mockResolvedValue(jsonResponse(204));
    const client = createClient(mock);

    await client.post('/api/a');
    await client.post('/api/b');

    const sessionCalls = mock.mock.calls.filter((call) => String(call[0]) === '/api/session');
    expect(sessionCalls).toHaveLength(1);
    expect(headerOf(mock, 2, 'X-CSRF-Token')).toBe('token-1');
  });

  it('shares one session request between concurrent unsafe requests', async () => {
    const mock: FetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(jsonResponse(200, { csrfToken: 'token-1' }))
      .mockResolvedValue(jsonResponse(204));
    const client = createClient(mock);

    await Promise.all([client.post('/api/a'), client.post('/api/b')]);

    const sessionCalls = mock.mock.calls.filter((call) => String(call[0]) === '/api/session');
    expect(sessionCalls).toHaveLength(1);
  });

  it('refreshes the token and retries once when a cached token gets 403', async () => {
    const mock: FetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(jsonResponse(200, { csrfToken: 'old' }))
      .mockResolvedValueOnce(jsonResponse(204))
      .mockResolvedValueOnce(jsonResponse(403))
      .mockResolvedValueOnce(jsonResponse(200, { csrfToken: 'new' }))
      .mockResolvedValueOnce(jsonResponse(204));
    const client = createClient(mock);

    await client.post('/api/first');
    await client.post('/api/second');

    expect(headerOf(mock, 2, 'X-CSRF-Token')).toBe('old');
    expect(urlOf(mock, 3)).toBe('/api/session');
    expect(headerOf(mock, 4, 'X-CSRF-Token')).toBe('new');
  });

  it('does not retry a 403 received with a freshly fetched token', async () => {
    const mock: FetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(jsonResponse(200, { csrfToken: 'fresh' }))
      .mockResolvedValueOnce(jsonResponse(403, { error: 'forbidden' }));
    const client = createClient(mock);

    await expect(client.post('/api/forbidden')).rejects.toMatchObject({ status: 403 });
    expect(mock).toHaveBeenCalledTimes(2);
  });

  it('reports a missing session on 401 and throws ApiError', async () => {
    const mock: FetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(401));
    const onSessionMissing = vi.fn();
    const client = createClient(mock, onSessionMissing);

    const failure = client.get('/api/servers');

    await expect(failure).rejects.toBeInstanceOf(ApiError);
    expect(onSessionMissing).toHaveBeenCalledTimes(1);
  });

  it('serializes bodies as JSON', async () => {
    const mock: FetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(jsonResponse(200, { csrfToken: 't' }))
      .mockResolvedValueOnce(jsonResponse(204));
    const client = createClient(mock);

    await client.post('/api/things', { name: 'x' });

    expect(mock.mock.calls[1]?.[1]?.body).toBe('{"name":"x"}');
    expect(headerOf(mock, 1, 'Content-Type')).toBe('application/json');
  });

  it('rejects paths outside /api', async () => {
    const mock: FetchMock = vi.fn<typeof fetch>();
    const client = createClient(mock);

    await expect(client.get('/other')).rejects.toThrow('/api');
    expect(mock).not.toHaveBeenCalled();
  });
});

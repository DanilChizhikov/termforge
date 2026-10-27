import { describe, expect, it, vi } from 'vitest';
import { ApiClient, ApiError } from './client';
import { buildSearchUrl, createLocalSearch, createRemoteSearch, readSearchStream, type FileSearchEvent } from './fileSearch';

function chunkedResponse(chunks: string[]): Response {
  const encoder = new TextEncoder();
  const body = new ReadableStream<Uint8Array>({
    start(controller) {
      for (const chunk of chunks) {
        controller.enqueue(encoder.encode(chunk));
      }
      controller.close();
    },
  });
  return new Response(body, { status: 200, headers: { 'Content-Type': 'application/x-ndjson' } });
}

const MATCH_LINE = JSON.stringify({
  type: 'match',
  entry: { name: 'app.log', path: '/srv/app.log', kind: 'File', size: 3, isBrokenLink: false },
  parentPath: '/srv',
});
const COMPLETED_LINE = JSON.stringify({
  type: 'completed',
  summary: {
    outcome: 'ResultLimitReached',
    method: 'Sftp',
    matchCount: 1,
    warningCount: 0,
    depthLimitReached: false,
    isPartial: true,
  },
});

describe('readSearchStream', () => {
  it('emits records split across chunk boundaries', async () => {
    const text = `${MATCH_LINE}\n${COMPLETED_LINE}\n`;
    const response = chunkedResponse([text.slice(0, 17), text.slice(17, 90), text.slice(90)]);
    const events: FileSearchEvent[] = [];

    await readSearchStream(response, (event) => events.push(event));

    expect(events.map((event) => event.type)).toEqual(['match', 'completed']);
    const match = events[0];
    expect(match?.type === 'match' && match.entry.modifiedUtc).toBeNull();
    const completed = events[1];
    expect(completed?.type === 'completed' && completed.summary.isPartial).toBe(true);
  });

  it('emits the last record without a trailing newline', async () => {
    const response = chunkedResponse([COMPLETED_LINE]);
    const events: FileSearchEvent[] = [];

    await readSearchStream(response, (event) => events.push(event));

    expect(events).toHaveLength(1);
  });
});

describe('buildSearchUrl', () => {
  it('encodes pattern and limits as query parameters', () => {
    const url = buildSearchUrl('/api/files/local/search', {
      path: '/srv/my app',
      pattern: "a'; rm -rf / #",
      maxDepth: 3,
      maxResults: 10,
      method: 'find',
    });

    const parameters = new URL(url, 'http://localhost').searchParams;
    expect(parameters.get('path')).toBe('/srv/my app');
    expect(parameters.get('pattern')).toBe("a'; rm -rf / #");
    expect(parameters.get('maxDepth')).toBe('3');
    expect(parameters.get('maxResults')).toBe('10');
    expect(parameters.get('method')).toBe('find');
  });
});

describe('search sources', () => {
  it('streams a remote search with the abort signal', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(chunkedResponse([`${COMPLETED_LINE}\n`]));
    const client = new ApiClient({ fetchImpl: fetchMock });
    const search = createRemoteSearch('abc', client);
    const controller = new AbortController();
    const onEvent = vi.fn();

    await search.search({ path: '/srv', pattern: 'x' }, onEvent, controller.signal);

    expect(search.supportsRemoteFind).toBe(true);
    expect(String(fetchMock.mock.calls[0]?.[0])).toBe('/api/files/remote/abc/search?path=%2Fsrv&pattern=x');
    expect(fetchMock.mock.calls[0]?.[1]?.signal).toBe(controller.signal);
    expect(onEvent).toHaveBeenCalledTimes(1);
  });

  it('rejects with the problem details when the search cannot start', async () => {
    const problem = new Response(JSON.stringify({ detail: 'Search pattern must not be empty.' }), { status: 400 });
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(problem);
    const client = new ApiClient({ fetchImpl: fetchMock });
    const search = createLocalSearch(client);

    const started = search.search({ path: '/srv', pattern: ' ' }, vi.fn(), new AbortController().signal);

    await expect(started).rejects.toBeInstanceOf(ApiError);
    expect(search.supportsRemoteFind).toBe(false);
  });
});

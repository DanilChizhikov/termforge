import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from './client';
import { deleteRecording, parseCast, startSessionRecording } from './recordings';

function clientRecording(calls: string[]): ApiClient {
  const fetchImpl = vi.fn((input: RequestInfo | URL) => {
    const url = typeof input === 'string' ? input : input.toString();
    calls.push(url);
    if (url === '/api/session') {
      return Promise.resolve(new Response(JSON.stringify({ csrfToken: 'token' }), { status: 200 }));
    }
    return Promise.resolve(new Response(JSON.stringify({ recording: true }), { status: 200 }));
  });
  return new ApiClient({ fetchImpl: fetchImpl as unknown as typeof fetch });
}

describe('recordings api', () => {
  it('parses asciicast v2 header and output events', () => {
    const text = [
      '{"version":2,"width":120,"height":40,"timestamp":1792929872}',
      '[0.25,"o","hello\\r\\n"]',
      '[1.5,"o","\\u001b[32mok\\u001b[0m"]',
      '',
    ].join('\n');

    expect(parseCast(text)).toEqual({
      width: 120,
      height: 40,
      events: [
        { time: 0.25, data: 'hello\r\n' },
        { time: 1.5, data: '\u001b[32mok\u001b[0m' },
      ],
    });
  });

  it('skips events that are not output events', () => {
    const text = ['{"version":2,"width":80,"height":24}', '[0.1,"i","typed"]', 'garbage', '[0.2,"o","shown"]'].join('\n');

    expect(parseCast(text)?.events).toEqual([{ time: 0.2, data: 'shown' }]);
  });

  it('rejects files that are not asciicast v2', () => {
    expect(parseCast('')).toBeNull();
    expect(parseCast('{"version":1,"width":80,"height":24}')).toBeNull();
    expect(parseCast('{"version":2,"width":0,"height":24}')).toBeNull();
    expect(parseCast('not json')).toBeNull();
  });

  it('encodes ids so they cannot change the request path', async () => {
    const calls: string[] = [];
    const client = clientRecording(calls);

    await startSessionRecording('../../x', client);
    await deleteRecording('../settings', client);

    expect(calls).toContain('/api/terminals/..%2F..%2Fx/recording');
    expect(calls).toContain('/api/recordings/..%2Fsettings');
  });
});

import { describe, expect, it } from 'vitest';
import { containerLogsUrl, parseLogMessage } from './docker';

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
});

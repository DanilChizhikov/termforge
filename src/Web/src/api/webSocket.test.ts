import { describe, expect, it } from 'vitest';
import { buildWebSocketUrl } from './webSocket';

describe('buildWebSocketUrl', () => {
  it('uses ws on http origins and the current host', () => {
    const url = buildWebSocketUrl('/api/terminals/1', {}, { protocol: 'http:', host: '127.0.0.1:5173' });

    expect(url).toBe('ws://127.0.0.1:5173/api/terminals/1');
  });

  it('uses wss on https origins', () => {
    const url = buildWebSocketUrl('/api/terminals/1', {}, { protocol: 'https:', host: 'localhost:5023' });

    expect(url).toBe('wss://localhost:5023/api/terminals/1');
  });

  it('appends an encoded query string', () => {
    const url = buildWebSocketUrl(
      '/api/terminals/1',
      { cols: 80, rows: 24, name: 'a b' },
      { protocol: 'http:', host: '127.0.0.1:5023' },
    );

    expect(url).toBe('ws://127.0.0.1:5023/api/terminals/1?cols=80&rows=24&name=a+b');
  });

  it('rejects paths outside /api', () => {
    expect(() => buildWebSocketUrl('/ws', {}, { protocol: 'http:', host: 'x' })).toThrow('/api/');
  });
});

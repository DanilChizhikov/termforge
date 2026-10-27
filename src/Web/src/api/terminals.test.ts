import { describe, expect, it } from 'vitest';
import { parseServerMessage, resizeMessage, terminalSocketUrl } from './terminals';

describe('terminal protocol', () => {
  it('builds a per-session socket url', () => {
    expect(terminalSocketUrl('abc', { protocol: 'http:', host: '127.0.0.1:5023' })).toBe(
      'ws://127.0.0.1:5023/api/terminals/abc/ws',
    );
  });

  it('serializes resize control messages', () => {
    expect(resizeMessage({ columns: 120, rows: 33 })).toBe('{"type":"resize","columns":120,"rows":33}');
  });

  it.each([
    ['{"type":"status","state":"connected"}', { type: 'status', state: 'connected' }],
    ['{"type":"status","state":"disconnected","reason":"gone"}', { type: 'status', state: 'disconnected', reason: 'gone' }],
    ['{"type":"status","state":"disconnected"}', { type: 'status', state: 'disconnected', reason: null }],
    ['{"type":"error","message":"bad"}', { type: 'error', message: 'bad' }],
  ])('parses %s', (text, expected) => {
    expect(parseServerMessage(text)).toEqual(expected);
  });

  it.each(['not json', '42', 'null', '{"type":"status","state":"weird"}', '{"type":"other"}'])(
    'ignores %s',
    (text) => {
      expect(parseServerMessage(text)).toBeNull();
    },
  );
});

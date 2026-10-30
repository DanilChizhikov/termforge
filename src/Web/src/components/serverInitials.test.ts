import { describe, expect, it } from 'vitest';
import { serverInitials } from './serverInitials';

describe('serverInitials', () => {
  it.each([
    ['web-01', 'w1'],
    ['db-01', 'd1'],
    ['edge-proxy', 'ep'],
    ['nas', 'na'],
    ['old-vps', 'ov'],
    ['Prod API 12', 'p1'],
    ['x', 'x'],
    ['--', '?'],
  ])('abbreviates %s as %s', (name, initials) => {
    expect(serverInitials(name)).toBe(initials);
  });
});

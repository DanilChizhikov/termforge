import { describe, expect, it } from 'vitest';
import { sudoBadgeText } from './sudoBadge';

describe('sudoBadgeText', () => {
  it('names the owner when the session user cannot write the file', () => {
    expect(sudoBadgeText({ ownerId: 0, ownerName: 'root', writable: false }, false)).toBe('owned by root · save needs sudo');
  });

  it('falls back to the uid when the owner name is unknown', () => {
    expect(sudoBadgeText({ ownerId: 0, ownerName: null, writable: false }, false)).toBe('owned by uid 0 · save needs sudo');
  });

  it('shows nothing for a writable file or an unknown access', () => {
    expect(sudoBadgeText({ ownerId: 1000, ownerName: 'deploy', writable: true }, false)).toBeNull();
    expect(sudoBadgeText(null, false)).toBeNull();
  });

  it('keeps the slot as an elevated indicator after opting in', () => {
    expect(sudoBadgeText({ ownerId: 0, ownerName: 'root', writable: false }, true)).toBe('owned by root · editing with sudo');
    expect(sudoBadgeText({ ownerId: 1000, ownerName: 'deploy', writable: true }, true)).toBe('owned by deploy · editing with sudo');
    expect(sudoBadgeText(null, true)).toBe('editing with sudo');
  });
});

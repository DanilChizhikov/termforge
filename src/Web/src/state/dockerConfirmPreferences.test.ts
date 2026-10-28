import { afterEach, describe, expect, it, vi } from 'vitest';
import { isConfirmationSkipped, reloadConfirmationPreferences, setConfirmationSkipped } from './dockerConfirmPreferences';

afterEach(() => {
  vi.restoreAllMocks();
  window.localStorage.clear();
  reloadConfirmationPreferences();
});

describe('dockerConfirmPreferences', () => {
  it('asks by default and remembers a skipped confirmation in local storage', () => {
    expect(isConfirmationSkipped('containerRemove')).toBe(false);

    setConfirmationSkipped('containerRemove', true);

    expect(isConfirmationSkipped('containerRemove')).toBe(true);
    expect(isConfirmationSkipped('imageRemove')).toBe(false);
    reloadConfirmationPreferences();
    expect(isConfirmationSkipped('containerRemove')).toBe(true);
  });

  it('asks again after the confirmation is turned back on', () => {
    setConfirmationSkipped('imageRemove', true);

    setConfirmationSkipped('imageRemove', false);
    reloadConfirmationPreferences();

    expect(isConfirmationSkipped('imageRemove')).toBe(false);
  });

  it('keeps asking when local storage is unavailable', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked');
    });

    setConfirmationSkipped('containerRemove', true);

    expect(isConfirmationSkipped('containerRemove')).toBe(false);
  });
});

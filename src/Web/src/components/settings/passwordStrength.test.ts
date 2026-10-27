import { describe, expect, it } from 'vitest';
import { measurePassword } from './passwordStrength';

describe('measurePassword', () => {
  it('treats passwords below the minimum length as too short', () => {
    expect(measurePassword('', 12)).toEqual({ score: 0, label: 'Too short' });
    expect(measurePassword('Short1!', 12).score).toBe(0);
  });

  it('rates longer and more varied passwords higher', () => {
    expect(measurePassword('aaaaaaaaaaaa', 12).score).toBe(1);
    expect(measurePassword('correct-horse-staple', 12)).toEqual({ score: 3, label: 'Strong' });
    expect(measurePassword('Correct-Horse-Staple-9', 12)).toEqual({ score: 4, label: 'Very strong' });
  });
});

import { describe, expect, it } from 'vitest';
import { formatBytes, formatUptime, percentOf, unitIndexOf } from './metricsFormat';

describe('metricsFormat', () => {
  it('formats uptime with the two most significant units', () => {
    expect(formatUptime(12 * 86400 + 4 * 3600)).toBe('12 d 4 h');
    expect(formatUptime(2 * 3600 + 5 * 60)).toBe('2 h 5 m');
    expect(formatUptime(90)).toBe('1 m');
  });

  it('formats bytes in a shared unit', () => {
    const gib = 1024 ** 3;
    const unit = unitIndexOf(80 * gib);

    expect(formatBytes(25 * gib, unit)).toEqual({ amount: '25.0', unit: 'GB' });
    expect(formatBytes(512)).toEqual({ amount: '512', unit: 'B' });
  });

  it('clamps percentages and tolerates an empty total', () => {
    expect(percentOf(5, 0)).toBe(0);
    expect(percentOf(150, 100)).toBe(100);
    expect(percentOf(1, 4)).toBe(25);
  });
});

import { describe, expect, it } from 'vitest';
import {
  formatClock,
  formatClockWithSeconds,
  formatCompactTimestamp,
  formatDayAndClock,
  formatElapsed,
  formatRelativeTime,
} from './timeFormat';

const NOW = new Date(2026, 9, 25, 14, 5, 0);

describe('timeFormat', () => {
  it('formats clock times in 24 hours', () => {
    const time = new Date(2026, 9, 25, 9, 4, 7);

    expect(formatClock(time)).toBe('09:04');
    expect(formatClockWithSeconds(time)).toBe('09:04:07');
  });

  it('shows the clock for today and the date otherwise', () => {
    expect(formatCompactTimestamp(new Date(2026, 9, 25, 14, 1), NOW)).toBe('14:01');
    expect(formatCompactTimestamp(new Date(2026, 9, 23, 8, 0), NOW)).toBe('Oct 23');
    expect(formatDayAndClock(new Date(2026, 9, 25, 14, 2), NOW)).toBe('Today 14:02');
  });

  it('describes past moments relative to now', () => {
    expect(formatRelativeTime(new Date(2026, 9, 22, 14, 5), NOW)).toBe('3 days ago');
    expect(formatRelativeTime(new Date(2026, 9, 25, 12, 5), NOW)).toBe('2 hours ago');
  });

  it('formats elapsed durations', () => {
    expect(formatElapsed(4_000)).toBe('4 s');
    expect(formatElapsed(14 * 60_000)).toBe('14 m');
    expect(formatElapsed((2 * 60 + 14) * 60_000)).toBe('2 h 14 m');
  });
});

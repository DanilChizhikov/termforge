import { describe, expect, it } from 'vitest';
import {
  baseName,
  canResume,
  describeAmount,
  describeRate,
  formatBytes,
  formatDuration,
  percentOf,
  type TransferRecord,
} from './transfers';

function makeTransfer(overrides: Partial<TransferRecord> = {}): TransferRecord {
  return {
    id: 't-1',
    connectionId: 'c-1',
    server: 'web-01',
    direction: 'download',
    sourcePath: '/srv/assets/',
    targetPath: '/home/me/assets',
    status: 'running',
    transferredBytes: 0,
    totalBytes: 0,
    completedFiles: 0,
    totalFiles: 1,
    bytesPerSecond: 0,
    remainingSeconds: null,
    error: null,
    retryable: false,
    partialBytes: 0,
    ...overrides,
  };
}

describe('transfer formatting', () => {
  it('formats bytes with binary units', () => {
    expect(formatBytes(512)).toBe('512 B');
    expect(formatBytes(1536)).toBe('1.5 KB');
    expect(formatBytes(5 * 1024 * 1024)).toBe('5.0 MB');
  });

  it('formats remaining time', () => {
    expect(formatDuration(0.2)).toBe('1 s');
    expect(formatDuration(75)).toBe('1 min 15 s');
    expect(formatDuration(3725)).toBe('1 h 2 min');
  });

  it('computes percent and treats empty succeeded transfers as complete', () => {
    expect(percentOf(makeTransfer({ transferredBytes: 1, totalBytes: 3 }))).toBe(33);
    expect(percentOf(makeTransfer({ status: 'succeeded' }))).toBe(100);
    expect(percentOf(makeTransfer())).toBe(0);
  });

  it('describes file counts for directory transfers', () => {
    const transfer = makeTransfer({ transferredBytes: 1024, totalBytes: 2048, completedFiles: 3, totalFiles: 10 });

    expect(describeAmount(transfer)).toBe('3 of 10 files');
  });

  it('describes rate only while running', () => {
    expect(describeRate(makeTransfer({ bytesPerSecond: 2048, remainingSeconds: 30 }))).toBe('2.0 KB/s · 30 s left');
    expect(describeRate(makeTransfer({ bytesPerSecond: 2048 }))).toBe('2.0 KB/s');
    expect(describeRate(makeTransfer({ status: 'failed', bytesPerSecond: 2048 }))).toBe('');
  });

  it('allows resume only for retryable finished transfers', () => {
    expect(canResume(makeTransfer({ status: 'failed', retryable: true }))).toBe(true);
    expect(canResume(makeTransfer({ status: 'cancelled', retryable: true }))).toBe(true);
    expect(canResume(makeTransfer({ status: 'failed' }))).toBe(false);
    expect(canResume(makeTransfer({ retryable: true }))).toBe(false);
  });

  it('takes the last path segment for both separators', () => {
    expect(baseName('/srv/assets/')).toBe('assets');
    expect(baseName('C:\\Users\\me\\file.txt')).toBe('file.txt');
  });
});

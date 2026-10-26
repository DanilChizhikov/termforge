const BYTE_UNITS = ['B', 'KB', 'MB', 'GB', 'TB', 'PB'];
const BYTES_PER_UNIT = 1024;
const SECONDS_PER_MINUTE = 60;
const SECONDS_PER_HOUR = 3600;
const SECONDS_PER_DAY = 86400;

export interface ByteSize {
  amount: string;
  unit: string;
}

export function formatBytes(bytes: number, unitIndex?: number): ByteSize {
  let index = unitIndex ?? 0;
  let amount = bytes;
  if (unitIndex === undefined) {
    while (amount >= BYTES_PER_UNIT && index < BYTE_UNITS.length - 1) {
      amount /= BYTES_PER_UNIT;
      index += 1;
    }
  } else {
    amount = bytes / BYTES_PER_UNIT ** unitIndex;
  }
  const digits = amount >= 100 || index === 0 ? 0 : 1;
  return { amount: amount.toFixed(digits), unit: BYTE_UNITS[index] ?? 'B' };
}

export function unitIndexOf(bytes: number): number {
  let index = 0;
  let amount = bytes;
  while (amount >= BYTES_PER_UNIT && index < BYTE_UNITS.length - 1) {
    amount /= BYTES_PER_UNIT;
    index += 1;
  }
  return index;
}

export function formatUptime(totalSeconds: number): string {
  const days = Math.floor(totalSeconds / SECONDS_PER_DAY);
  const hours = Math.floor((totalSeconds % SECONDS_PER_DAY) / SECONDS_PER_HOUR);
  const minutes = Math.floor((totalSeconds % SECONDS_PER_HOUR) / SECONDS_PER_MINUTE);
  if (days > 0) {
    return `${days} d ${hours} h`;
  }
  if (hours > 0) {
    return `${hours} h ${minutes} m`;
  }
  return `${minutes} m`;
}

export function percentOf(part: number, total: number): number {
  if (total <= 0) {
    return 0;
  }
  return Math.min(100, Math.max(0, Math.round((part / total) * 100)));
}

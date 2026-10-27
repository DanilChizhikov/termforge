const MINUTE_MS = 60_000;
const HOUR_MS = 60 * MINUTE_MS;
const DAY_MS = 24 * HOUR_MS;

const CLOCK_FORMAT = new Intl.DateTimeFormat('en-GB', { hour: '2-digit', minute: '2-digit' });
const CLOCK_SECONDS_FORMAT = new Intl.DateTimeFormat('en-GB', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
const SHORT_DATE_FORMAT = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric' });
const RELATIVE_FORMAT = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });

function toDate(value: string | Date): Date {
  return typeof value === 'string' ? new Date(value) : value;
}

function isSameDay(left: Date, right: Date): boolean {
  return (
    left.getFullYear() === right.getFullYear() &&
    left.getMonth() === right.getMonth() &&
    left.getDate() === right.getDate()
  );
}

export function formatClock(value: string | Date): string {
  return CLOCK_FORMAT.format(toDate(value));
}

export function formatClockWithSeconds(value: string | Date): string {
  return CLOCK_SECONDS_FORMAT.format(toDate(value));
}

export function formatShortDate(value: string | Date): string {
  return SHORT_DATE_FORMAT.format(toDate(value));
}

export function formatCompactTimestamp(value: string | Date, now: Date = new Date()): string {
  const date = toDate(value);
  return isSameDay(date, now) ? formatClock(date) : formatShortDate(date);
}

export function formatDayAndClock(value: string | Date, now: Date = new Date()): string {
  const date = toDate(value);
  const day = isSameDay(date, now) ? 'Today' : formatShortDate(date);
  return `${day} ${formatClock(date)}`;
}

export function formatRelativeTime(value: string | Date, now: Date = new Date()): string {
  const elapsed = toDate(value).getTime() - now.getTime();
  const magnitude = Math.abs(elapsed);
  if (magnitude < HOUR_MS) {
    return RELATIVE_FORMAT.format(Math.round(elapsed / MINUTE_MS), 'minute');
  }
  if (magnitude < DAY_MS) {
    return RELATIVE_FORMAT.format(Math.round(elapsed / HOUR_MS), 'hour');
  }
  return RELATIVE_FORMAT.format(Math.round(elapsed / DAY_MS), 'day');
}

export function formatElapsed(milliseconds: number): string {
  const totalMinutes = Math.max(0, Math.floor(milliseconds / MINUTE_MS));
  if (totalMinutes < 1) {
    return `${Math.max(0, Math.floor(milliseconds / 1000))} s`;
  }
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return hours === 0 ? `${minutes} m` : `${hours} h ${minutes} m`;
}

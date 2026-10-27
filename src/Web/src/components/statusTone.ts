export type StatusTone = 'ok' | 'active' | 'caution' | 'danger' | 'idle';

export type StatusDotSize = 'md' | 'sm' | 'xs';

export function statusDotClass(tone: StatusTone, size: StatusDotSize = 'md'): string {
  const sizeClass = size === 'md' ? '' : ` status-dot-${size}`;
  return `status-dot${sizeClass} status-dot-${tone}`;
}

export function statusTextClass(tone: StatusTone): string {
  return `status-text status-text-${tone}`;
}

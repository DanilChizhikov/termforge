import type { ContainerAction, DockerAvailabilityState, DockerContainer } from '../../api/docker';
import type { StatusTone } from '../statusTone';

export type { StatusTone } from '../statusTone';

export type ContainerFilter = 'all' | 'running' | 'problems';

const EXIT_CODE_PATTERN = /^Exited \((\d+)\)/i;
const EXITED_PATTERN = /^Exited \((\d+)\)\s*(.*)$/i;
const UP_PATTERN = /^Up\s+(.+?)(?:\s+\((?:healthy|unhealthy|health: starting)\))?$/i;
const SHORT_UNITS: ReadonlyArray<readonly [RegExp, string]> = [
  [/^less than a second$/i, '1 s'],
  [/^about a minute$/i, '1 min'],
  [/^about an hour$/i, '1 h'],
  [/^(\d+) seconds?$/i, '$1 s'],
  [/^(\d+) minutes?$/i, '$1 min'],
  [/^(\d+) hours?$/i, '$1 h'],
];

function shortenDuration(duration: string): string {
  const trimmed = duration.trim();
  for (const [pattern, replacement] of SHORT_UNITS) {
    if (pattern.test(trimmed)) {
      return trimmed.replace(pattern, replacement);
    }
  }
  return trimmed;
}

export function containerUptime(container: DockerContainer): string {
  if (container.state !== 'Running') {
    return '—';
  }
  const match = UP_PATTERN.exec(container.status);
  return match === null ? '—' : shortenDuration(match[1] ?? '');
}

export function containerTone(container: DockerContainer): StatusTone {
  if (container.state === 'Dead' || container.health === 'unhealthy') {
    return 'danger';
  }
  if (container.state === 'Restarting' || container.state === 'Paused' || container.state === 'Removing') {
    return 'caution';
  }
  if (container.state === 'Running') {
    return container.health === 'starting' ? 'caution' : 'ok';
  }
  return exitCode(container) > 0 ? 'danger' : 'idle';
}

export function containerStateLabel(container: DockerContainer): string {
  const state = container.state.toLowerCase();
  const exited = EXITED_PATTERN.exec(container.status);
  if (container.state === 'Exited' && exited !== null) {
    const ago = (exited[2] ?? '').replace(/\s+ago$/i, '');
    return ago === '' ? `exited (${exited[1]})` : `exited (${exited[1]}) · ${shortenDuration(ago)} ago`;
  }
  return container.health === null ? state : `${state} · ${container.health}`;
}

export function isRunning(container: DockerContainer): boolean {
  return container.state === 'Running';
}

export function isProblem(container: DockerContainer): boolean {
  const tone = containerTone(container);
  return tone === 'danger' || container.state === 'Restarting';
}

export function matchesFilter(container: DockerContainer, filter: ContainerFilter): boolean {
  if (filter === 'running') {
    return isRunning(container);
  }
  if (filter === 'problems') {
    return isProblem(container);
  }
  return true;
}

export function availableActions(container: DockerContainer): ContainerAction[] {
  if (container.state === 'Running' || container.state === 'Paused' || container.state === 'Restarting') {
    return ['restart', 'stop'];
  }
  if (container.state === 'Created' || container.state === 'Exited' || container.state === 'Dead') {
    return ['start'];
  }
  return [];
}

export function canOpenShell(container: DockerContainer): boolean {
  return isRunning(container);
}

export function offersShell(container: DockerContainer): boolean {
  return isRunning(container) || container.state === 'Restarting';
}

export function availabilityTone(state: DockerAvailabilityState): StatusTone {
  if (state === 'Available') {
    return 'ok';
  }
  return state === 'Unknown' ? 'idle' : 'danger';
}

export const AVAILABILITY_TITLES: Record<DockerAvailabilityState, string> = {
  Available: 'Docker reachable',
  NotInstalled: 'Docker is not installed',
  NoAccess: 'Docker is not accessible',
  Unknown: 'Docker status unknown',
};

export const ACTION_LABELS: Record<ContainerAction, string> = {
  start: 'Start',
  stop: 'Stop',
  restart: 'Restart',
};

export const ACTION_PAST_TENSE: Record<ContainerAction, string> = {
  start: 'Started',
  stop: 'Stopped',
  restart: 'Restarted',
};

function exitCode(container: DockerContainer): number {
  const match = EXIT_CODE_PATTERN.exec(container.status);
  return match === null ? 0 : Number(match[1]);
}

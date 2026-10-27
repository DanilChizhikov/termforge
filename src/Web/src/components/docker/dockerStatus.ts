import type { ContainerAction, DockerAvailabilityState, DockerContainer } from '../../api/docker';
import type { StatusTone } from '../statusTone';

export type { StatusTone } from '../statusTone';

export type ContainerFilter = 'all' | 'running' | 'problems';

const EXIT_CODE_PATTERN = /^Exited \((\d+)\)/i;

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

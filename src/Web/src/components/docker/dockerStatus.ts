import type {
  ComposeAction,
  ComposeInfo,
  ContainerAction,
  DockerAvailabilityState,
  DockerContainer,
  DockerImage,
} from '../../api/docker';
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
    return ['restart', 'stop', 'remove'];
  }
  if (container.state === 'Created' || container.state === 'Exited' || container.state === 'Dead') {
    return ['start', 'remove'];
  }
  return container.state === 'Removing' ? [] : ['remove'];
}

export interface ContainerGroup {
  compose: ComposeInfo | null;
  containers: DockerContainer[];
}

export function groupByComposeProject(containers: DockerContainer[]): ContainerGroup[] {
  const projects = new Map<string, ContainerGroup>();
  const standalone: DockerContainer[] = [];
  containers.forEach((container) => {
    const compose = container.compose ?? null;
    if (compose === null) {
      standalone.push(container);
      return;
    }
    const group = projects.get(compose.project);
    if (group === undefined) {
      projects.set(compose.project, { compose, containers: [container] });
      return;
    }
    group.containers.push(container);
  });
  const groups = [...projects.values()];
  return standalone.length > 0 ? [...groups, { compose: null, containers: standalone }] : groups;
}

const NONE = '<none>';
const SHORT_ID_LENGTH = 12;

export function imageReference(image: DockerImage): string {
  if (image.repository === NONE || image.repository === '') {
    return image.id;
  }
  return image.tag === NONE || image.tag === '' ? image.repository : `${image.repository}:${image.tag}`;
}

export function shortImageId(id: string): string {
  const hash = id.startsWith('sha256:') ? id.slice('sha256:'.length) : id;
  return hash.slice(0, SHORT_ID_LENGTH);
}

export function imageUsage(image: DockerImage, containers: DockerContainer[]): number {
  const names = new Set<string>([image.id, shortImageId(image.id)]);
  if (image.repository !== NONE) {
    names.add(imageReference(image));
    if (image.tag === 'latest') {
      names.add(image.repository);
    }
  }
  return containers.filter((container) => names.has(container.image)).length;
}

export const COMPOSE_ACTION_LABELS: Record<ComposeAction, string> = {
  restart: 'Restart',
  recreate: 'Recreate',
};

export const COMPOSE_ACTION_PAST_TENSE: Record<ComposeAction, string> = {
  restart: 'Restarted',
  recreate: 'Recreated',
};

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
  remove: 'Remove',
};

export const ACTION_PAST_TENSE: Record<ContainerAction, string> = {
  start: 'Started',
  stop: 'Stopped',
  restart: 'Restarted',
  remove: 'Removed',
};

function exitCode(container: DockerContainer): number {
  const match = EXIT_CODE_PATTERN.exec(container.status);
  return match === null ? 0 : Number(match[1]);
}

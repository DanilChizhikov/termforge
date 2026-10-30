import type { OperationRecord, OperationStatus } from '../api/operations';
import { baseName, parentDirectoryOf } from '../api/transfers';
import type { StatusTone } from './statusTone';

const KIND_LABELS: Record<string, string> = {
  'transfer.upload': 'Upload',
  'transfer.download': 'Download',
  'docker.start': 'Docker',
  'docker.stop': 'Docker',
  'docker.restart': 'Docker',
  'docker.remove': 'Docker',
  'docker.compose.restart': 'Docker',
  'docker.compose.recreate': 'Docker',
  'docker.image.remove': 'Docker',
  'docker.pull': 'Docker',
  'file.save': 'File save',
  'file.rollback': 'Rollback',
};

const DOCKER_ACTIONS: Record<string, string> = {
  start: 'Start',
  stop: 'Stop',
  restart: 'Restart',
  remove: 'Remove',
  recreate: 'Recreate',
  pull: 'Pull',
};

const DOCKER_KIND_ACTIONS: Record<string, string> = {
  'docker.compose.restart': 'restart',
  'docker.compose.recreate': 'recreate',
  'docker.image.remove': 'remove',
  'docker.pull': 'pull',
};

function describeDocker(kind: string, details: Record<string, string>): string {
  const actionKey = details.action ?? DOCKER_KIND_ACTIONS[kind] ?? '';
  const action = DOCKER_ACTIONS[actionKey] ?? actionKey;
  const target = details.container ?? details.project ?? details.image;
  return target === undefined ? action : `${action} ${target}`.trim();
}

export const OPERATION_STATUS_TONES: Record<OperationStatus, StatusTone> = {
  queued: 'idle',
  running: 'active',
  succeeded: 'ok',
  failed: 'danger',
  cancelled: 'idle',
};

const STATUS_WORDS: Record<OperationStatus, string> = {
  queued: 'Queued',
  running: 'Running',
  succeeded: 'Done',
  failed: 'Failed',
  cancelled: 'Cancelled',
};

export function describeOperationKind(kind: string): string {
  return KIND_LABELS[kind] ?? kind;
}

function describeTransfer(details: Record<string, string>): string | null {
  const source = details.source;
  if (source === undefined) {
    return null;
  }
  const target = details.targetDirectory ?? (details.target === undefined ? '' : parentDirectoryOf(details.target));
  const files = details.files === undefined ? '' : ` (${details.files.split('/').pop() ?? details.files} files)`;
  const directory = target.endsWith('/') || target === '' ? target : `${target}/`;
  return `${baseName(source)}${files} → ${directory}`;
}

export function describeOperationDetails(operation: OperationRecord): string {
  const { details } = operation;
  if (operation.kind.startsWith('transfer.')) {
    return describeTransfer(details) ?? '';
  }
  if (operation.kind.startsWith('docker.')) {
    return describeDocker(operation.kind, details);
  }
  if (operation.kind.startsWith('file.')) {
    const backup = details.backup === undefined ? '' : ' · backup kept';
    return `${details.path ?? ''}${backup}`;
  }
  return '';
}

function elapsedSeconds(operation: OperationRecord): number | null {
  if (operation.startedAt === null || operation.finishedAt === null) {
    return null;
  }
  const elapsed = (Date.parse(operation.finishedAt) - Date.parse(operation.startedAt)) / 1000;
  return Number.isFinite(elapsed) && elapsed >= 0 ? Math.max(1, Math.round(elapsed)) : null;
}

export function describeOperationStatus(operation: OperationRecord): string {
  const word = STATUS_WORDS[operation.status];
  if (operation.status !== 'succeeded') {
    return word;
  }
  const seconds = elapsedSeconds(operation);
  return seconds === null ? word : `${word} · ${seconds} s`;
}

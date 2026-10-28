import { apiClient, type ApiClient } from './client';
import { requestConfirmationToken, type DangerousOperation } from './confirmations';
import type { TerminalSession, TerminalSize } from './terminals';
import { buildWebSocketUrl, type LocationLike } from './webSocket';

export type DockerAvailabilityState = 'Available' | 'NotInstalled' | 'NoAccess' | 'Unknown';

export type ContainerState =
  | 'Unknown'
  | 'Created'
  | 'Running'
  | 'Paused'
  | 'Restarting'
  | 'Removing'
  | 'Exited'
  | 'Dead';

export type ContainerAction = 'start' | 'stop' | 'restart' | 'remove';

export type ComposeAction = 'restart' | 'recreate';

export type ContainerShellChoice = 'auto' | 'bash' | 'sh';

export interface DockerContainer {
  id: string;
  name: string;
  image: string;
  state: ContainerState;
  status: string;
  health: string | null;
  ports: string;
  createdAt: string;
  compose?: ComposeInfo | null;
}

export interface ComposeInfo {
  project: string;
  workingDirectory: string;
}

export interface DockerImage {
  id: string;
  repository: string;
  tag: string;
  size: string;
  createdSince: string;
}

export interface DockerImages {
  connectionId: string;
  serverName: string;
  images: DockerImage[];
}

export interface ImageRemoveResult {
  operationId: string;
  image: string;
}

export interface ComposeActionResult {
  operationId: string;
  project: string;
  action: ComposeAction;
}

export interface DockerOverview {
  connectionId: string;
  serverName: string;
  availability: { state: DockerAvailabilityState; reason: string };
  containers: DockerContainer[];
}

export interface ContainerActionResult {
  operationId: string;
  container: string;
  action: ContainerAction;
}

export interface ContainerLogQuery {
  tail: number;
  follow: boolean;
}

export type ContainerLogMessage =
  | { type: 'output'; text: string }
  | { type: 'end'; exitCode: number | null }
  | { type: 'error'; message: string };

export const LOG_TAIL_OPTIONS: readonly number[] = [100, 200, 1000, 5000];

export const ACTIONS_REQUIRING_CONFIRMATION: ReadonlySet<ContainerAction> = new Set(['stop', 'restart', 'remove']);

const DANGEROUS_ACTIONS: Partial<Record<ContainerAction, DangerousOperation>> = {
  stop: 'ContainerStop',
  restart: 'ContainerRestart',
  remove: 'ContainerRemove',
};

const COMPOSE_OPERATIONS: Record<ComposeAction, DangerousOperation> = {
  restart: 'ComposeRestart',
  recreate: 'ComposeRecreate',
};

function dockerPath(connectionId: string): string {
  return `/api/connections/${encodeURIComponent(connectionId)}/docker`;
}

function containerPath(connectionId: string, container: string): string {
  return `${dockerPath(connectionId)}/containers/${encodeURIComponent(container)}`;
}

export function getDockerOverview(connectionId: string, client: ApiClient = apiClient): Promise<DockerOverview> {
  return client.get<DockerOverview>(dockerPath(connectionId));
}

export async function runContainerAction(
  connectionId: string,
  container: string,
  action: ContainerAction,
  confirmed: boolean,
  client: ApiClient = apiClient,
): Promise<ContainerActionResult> {
  const operation = DANGEROUS_ACTIONS[action];
  const confirmationToken =
    operation !== undefined && confirmed
      ? await requestConfirmationToken({ operation, connectionId, target: container }, client)
      : null;
  return client.post<ContainerActionResult>(`${containerPath(connectionId, container)}/${action}`, { confirmationToken });
}

export function getDockerImages(connectionId: string, client: ApiClient = apiClient): Promise<DockerImages> {
  return client.get<DockerImages>(`${dockerPath(connectionId)}/images`);
}

export async function removeDockerImage(
  connectionId: string,
  image: string,
  client: ApiClient = apiClient,
): Promise<ImageRemoveResult> {
  const scope = { operation: 'ImageRemove' as const, connectionId, target: image };
  const confirmationToken = await requestConfirmationToken(scope, client);
  return client.post<ImageRemoveResult>(`${dockerPath(connectionId)}/images/remove`, { image, confirmationToken });
}

export async function runComposeAction(
  connectionId: string,
  project: string,
  action: ComposeAction,
  client: ApiClient = apiClient,
): Promise<ComposeActionResult> {
  const scope = { operation: COMPOSE_OPERATIONS[action], connectionId, target: project };
  const confirmationToken = await requestConfirmationToken(scope, client);
  const path = `${dockerPath(connectionId)}/compose/${encodeURIComponent(project)}/${action}`;
  return client.post<ComposeActionResult>(path, { confirmationToken });
}

export function imagePullUrl(connectionId: string, image: string, location?: LocationLike): string {
  const path = `${dockerPath(connectionId)}/images/pull/ws`;
  const parameters = { image };
  return location === undefined ? buildWebSocketUrl(path, parameters) : buildWebSocketUrl(path, parameters, location);
}

export function openContainerShell(
  connectionId: string,
  container: string,
  size: TerminalSize,
  client: ApiClient = apiClient,
): Promise<TerminalSession> {
  return client.post<TerminalSession>(`${containerPath(connectionId, container)}/shell`, {
    columns: size.columns,
    rows: size.rows,
    shell: 'auto',
  });
}

export function containerLogsUrl(
  connectionId: string,
  container: string,
  query: ContainerLogQuery,
  location?: LocationLike,
): string {
  const path = `${containerPath(connectionId, container)}/logs/ws`;
  const parameters = { tail: query.tail, follow: query.follow };
  return location === undefined ? buildWebSocketUrl(path, parameters) : buildWebSocketUrl(path, parameters, location);
}

export function parseLogMessage(text: string): ContainerLogMessage | null {
  let parsed: unknown;
  try {
    parsed = JSON.parse(text);
  } catch {
    return null;
  }
  if (typeof parsed !== 'object' || parsed === null) {
    return null;
  }
  const message = parsed as Record<string, unknown>;
  if (message['type'] === 'output' && typeof message['text'] === 'string') {
    return { type: 'output', text: message['text'] };
  }
  if (message['type'] === 'end') {
    const exitCode = typeof message['exitCode'] === 'number' ? message['exitCode'] : null;
    return { type: 'end', exitCode };
  }
  if (message['type'] === 'error' && typeof message['message'] === 'string') {
    return { type: 'error', message: message['message'] };
  }
  return null;
}

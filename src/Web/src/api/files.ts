import { ApiError, apiClient, type ApiClient } from './client';

export type FileKind = 'File' | 'Directory' | 'SymbolicLink' | 'Other';

export interface FileEntry {
  name: string;
  path: string;
  kind: FileKind;
  size: number;
  modifiedUtc: string | null;
  permissions: string | null;
  linkTargetKind: FileKind | null;
  isBrokenLink: boolean;
}

export interface DirectoryListing {
  path: string;
  parentPath: string | null;
  entries: FileEntry[];
}

export interface LocalRoot {
  name: string;
  path: string;
}

export interface FileSource {
  list(path: string | null): Promise<DirectoryListing>;
  createDirectory(parentPath: string, name: string): Promise<void>;
  rename(path: string, newName: string): Promise<void>;
  remove(path: string): Promise<void>;
}

export type SortKey = 'name' | 'size' | 'modified' | 'kind';
export type SortDirection = 'asc' | 'desc';

export interface Breadcrumb {
  label: string;
  path: string;
}

const LOCAL_PATH = '/api/files/local';

function createSource(basePath: string, client: ApiClient): FileSource {
  return {
    list(path) {
      const query = path === null ? '' : `?path=${encodeURIComponent(path)}`;
      return client.get<DirectoryListing>(`${basePath}/list${query}`);
    },
    async createDirectory(parentPath, name) {
      await client.post<void>(`${basePath}/directories`, { parentPath, name });
    },
    async rename(path, newName) {
      await client.post<void>(`${basePath}/rename`, { path, newName });
    },
    async remove(path) {
      await client.post<void>(`${basePath}/delete`, { path, confirm: true });
    },
  };
}

export function createLocalSource(client: ApiClient = apiClient): FileSource {
  return createSource(LOCAL_PATH, client);
}

export function createRemoteSource(connectionId: string, client: ApiClient = apiClient): FileSource {
  return createSource(`/api/files/remote/${encodeURIComponent(connectionId)}`, client);
}

export function getLocalRoots(client: ApiClient = apiClient): Promise<LocalRoot[]> {
  return client.get<LocalRoot[]>(`${LOCAL_PATH}/roots`);
}

export function describeFileError(error: unknown): string {
  if (error instanceof ApiError) {
    const body = error.body as { detail?: string } | undefined;
    return body?.detail ?? `Request failed with status ${error.status}.`;
  }
  return 'Request failed.';
}

export function isNavigable(entry: FileEntry): boolean {
  if (entry.kind === 'Directory') {
    return true;
  }
  return entry.kind === 'SymbolicLink' && entry.linkTargetKind === 'Directory';
}

const WINDOWS_DRIVE = /^[A-Za-z]:[\\/]?/;

export function breadcrumbsOf(path: string): Breadcrumb[] {
  const driveMatch = WINDOWS_DRIVE.exec(path);
  if (driveMatch !== null) {
    return windowsBreadcrumbs(path, driveMatch[0]);
  }
  const crumbs: Breadcrumb[] = [{ label: '/', path: '/' }];
  let current = '';
  for (const segment of path.split('/').filter((part) => part.length > 0)) {
    current = `${current}/${segment}`;
    crumbs.push({ label: segment, path: current });
  }
  return crumbs;
}

function windowsBreadcrumbs(path: string, driveToken: string): Breadcrumb[] {
  const drive = driveToken.slice(0, 2);
  const rootPath = `${drive}\\`;
  const crumbs: Breadcrumb[] = [{ label: drive, path: rootPath }];
  let current = drive;
  for (const segment of path.slice(driveToken.length).split(/[\\/]/).filter((part) => part.length > 0)) {
    current = `${current}\\${segment}`;
    crumbs.push({ label: segment, path: current });
  }
  return crumbs;
}

function compareBy(key: SortKey, left: FileEntry, right: FileEntry): number {
  switch (key) {
    case 'size':
      return left.size - right.size;
    case 'modified':
      return (left.modifiedUtc ?? '').localeCompare(right.modifiedUtc ?? '');
    case 'kind':
      return left.kind.localeCompare(right.kind);
    default:
      return left.name.localeCompare(right.name, undefined, { sensitivity: 'base' });
  }
}

export function sortEntries(
  entries: readonly FileEntry[],
  key: SortKey,
  direction: SortDirection,
): FileEntry[] {
  const factor = direction === 'asc' ? 1 : -1;
  return [...entries].sort((left, right) => {
    const leftIsDirectory = isNavigable(left);
    const rightIsDirectory = isNavigable(right);
    if (leftIsDirectory !== rightIsDirectory) {
      return leftIsDirectory ? -1 : 1;
    }
    const primary = compareBy(key, left, right) * factor;
    return primary !== 0 ? primary : left.name.localeCompare(right.name);
  });
}

export function formatSize(entry: FileEntry): string {
  if (entry.kind === 'Directory') {
    return '';
  }
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let value = entry.size;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit += 1;
  }
  const text = unit === 0 ? `${value}` : value.toFixed(1);
  return `${text} ${units[unit]}`;
}

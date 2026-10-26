import { describe, expect, it, vi } from 'vitest';
import { ApiError, type ApiClient } from './client';
import {
  breadcrumbsOf,
  createLocalSource,
  createRemoteSource,
  describeFileError,
  formatSize,
  sortEntries,
  type FileEntry,
} from './files';

function entry(overrides: Partial<FileEntry>): FileEntry {
  return {
    name: 'a',
    path: '/a',
    kind: 'File',
    size: 0,
    modifiedUtc: null,
    permissions: null,
    linkTargetKind: null,
    isBrokenLink: false,
    ...overrides,
  };
}

describe('breadcrumbsOf', () => {
  it('builds posix crumbs from the root', () => {
    expect(breadcrumbsOf('/home/user')).toEqual([
      { label: '/', path: '/' },
      { label: 'home', path: '/home' },
      { label: 'user', path: '/home/user' },
    ]);
  });

  it('returns only the root for /', () => {
    expect(breadcrumbsOf('/')).toEqual([{ label: '/', path: '/' }]);
  });

  it('builds windows crumbs from the drive', () => {
    expect(breadcrumbsOf('C:\\Users\\me')).toEqual([
      { label: 'C:', path: 'C:\\' },
      { label: 'Users', path: 'C:\\Users' },
      { label: 'me', path: 'C:\\Users\\me' },
    ]);
  });
});

describe('sortEntries', () => {
  const entries = [
    entry({ name: 'b.txt', path: '/b.txt', size: 5 }),
    entry({ name: 'zdir', path: '/zdir', kind: 'Directory' }),
    entry({ name: 'a.txt', path: '/a.txt', size: 10 }),
    entry({ name: 'link', path: '/link', kind: 'SymbolicLink', linkTargetKind: 'Directory' }),
  ];

  it('keeps directories first and sorts by name ascending', () => {
    const names = sortEntries(entries, 'name', 'asc').map((item) => item.name);

    expect(names).toEqual(['link', 'zdir', 'a.txt', 'b.txt']);
  });

  it('keeps directories first when sorting descending', () => {
    const names = sortEntries(entries, 'name', 'desc').map((item) => item.name);

    expect(names).toEqual(['zdir', 'link', 'b.txt', 'a.txt']);
  });

  it('sorts by size', () => {
    const names = sortEntries(entries, 'size', 'desc').map((item) => item.name);

    expect(names.slice(-2)).toEqual(['a.txt', 'b.txt']);
  });
});

describe('formatSize', () => {
  it('formats bytes and kilobytes', () => {
    expect(formatSize(entry({ size: 512 }))).toBe('512 B');
    expect(formatSize(entry({ size: 2048 }))).toBe('2.0 KB');
  });

  it('is empty for directories', () => {
    expect(formatSize(entry({ kind: 'Directory', size: 0 }))).toBe('');
  });
});

describe('describeFileError', () => {
  it('uses the problem detail', () => {
    const error = new ApiError(404, { detail: 'Path does not exist: /x' });

    expect(describeFileError(error)).toBe('Path does not exist: /x');
  });

  it('falls back to the status', () => {
    expect(describeFileError(new ApiError(500, undefined))).toContain('500');
  });
});

describe('sources', () => {
  function fakeClient(): ApiClient {
    return { get: vi.fn().mockResolvedValue({}), post: vi.fn().mockResolvedValue(undefined) } as unknown as ApiClient;
  }

  it('lists local paths with an encoded query', async () => {
    const client = fakeClient();

    await createLocalSource(client).list('/tmp/a b');

    expect(client.get).toHaveBeenCalledWith('/api/files/local/list?path=%2Ftmp%2Fa%20b');
  });

  it('lists the default location without a query', async () => {
    const client = fakeClient();

    await createRemoteSource('abc', client).list(null);

    expect(client.get).toHaveBeenCalledWith('/api/files/remote/abc/list');
  });

  it('sends explicit confirmation on delete', async () => {
    const client = fakeClient();

    await createRemoteSource('abc', client).remove('/x');

    expect(client.post).toHaveBeenCalledWith('/api/files/remote/abc/delete', { path: '/x', confirm: true });
  });

  it('sends rename and create requests', async () => {
    const client = fakeClient();
    const source = createLocalSource(client);

    await source.rename('/a', 'b');
    await source.createDirectory('/p', 'n');

    expect(client.post).toHaveBeenCalledWith('/api/files/local/rename', { path: '/a', newName: 'b' });
    expect(client.post).toHaveBeenCalledWith('/api/files/local/directories', { parentPath: '/p', name: 'n' });
  });
});

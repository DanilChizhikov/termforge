import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../api/client';
import type { DirectoryListing, FileEntry, FileSource } from '../api/files';
import type { FileSearchEvent, FileSearchQuery, FileSearchSource, FileSearchSummary } from '../api/fileSearch';
import { FilePane } from './FilePane';

function entry(overrides: Partial<FileEntry>): FileEntry {
  return {
    name: 'file.txt',
    path: '/srv/file.txt',
    kind: 'File',
    size: 1,
    modifiedUtc: null,
    permissions: null,
    linkTargetKind: null,
    isBrokenLink: false,
    ...overrides,
  };
}

const LISTINGS: Record<string, DirectoryListing> = {
  '/srv': { path: '/srv', parentPath: '/', entries: [entry({ name: 'logs', path: '/srv/logs', kind: 'Directory' })] },
  '/srv/logs': {
    path: '/srv/logs',
    parentPath: '/srv',
    entries: [entry({ name: 'app.log', path: '/srv/logs/app.log' }), entry({ name: 'db.log', path: '/srv/logs/db.log' })],
  },
};

function makeSource(): FileSource {
  return {
    list: vi.fn((path: string | null) => Promise.resolve(LISTINGS[path ?? '/srv'] ?? LISTINGS['/srv']!)),
    createDirectory: vi.fn().mockResolvedValue(undefined),
    rename: vi.fn().mockResolvedValue(undefined),
    remove: vi.fn().mockResolvedValue(undefined),
  };
}

function summary(overrides: Partial<FileSearchSummary>): FileSearchSummary {
  return {
    outcome: 'Completed',
    method: 'Sftp',
    matchCount: 0,
    warningCount: 0,
    depthLimitReached: false,
    isPartial: false,
    message: null,
    ...overrides,
  };
}

function scriptedSearch(events: FileSearchEvent[], supportsRemoteFind = false): FileSearchSource & { queries: FileSearchQuery[] } {
  const queries: FileSearchQuery[] = [];
  return {
    supportsRemoteFind,
    queries,
    search: vi.fn((query: FileSearchQuery, onEvent: (event: FileSearchEvent) => void) => {
      queries.push(query);
      events.forEach(onEvent);
      return Promise.resolve();
    }),
  };
}

function Harness({ source, search }: { source: FileSource; search: FileSearchSource }) {
  const [path, setPath] = useState<string | null>('/srv');
  return <FilePane title="Remote" sourceKey="test" source={source} path={path} onPathChange={setPath} search={search} />;
}

async function openSearchAndRun(pattern: string): Promise<ReturnType<typeof userEvent.setup>> {
  const user = userEvent.setup();
  await screen.findByText('logs');
  await user.click(screen.getByRole('button', { name: 'Search files' }));
  await user.type(screen.getByLabelText('Name pattern'), pattern);
  await user.click(screen.getByRole('button', { name: 'Search' }));
  return user;
}

describe('FilePane search', () => {
  it('streams matches and reports a complete search', async () => {
    const search = scriptedSearch([
      { type: 'match', entry: entry({ name: 'app.log', path: '/srv/logs/app.log' }), parentPath: '/srv/logs' },
      { type: 'completed', summary: summary({ matchCount: 1 }) },
    ]);
    render(<Harness source={makeSource()} search={search} />);

    await openSearchAndRun('*.log');

    const results = await screen.findByRole('list', { name: 'Remote search results' });
    expect(within(results).getByText('app.log')).toBeInTheDocument();
    expect(within(results).getByText('/srv/logs')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Complete: 1 match.');
    expect(search.queries[0]).toMatchObject({ path: '/srv', pattern: '*.log', maxDepth: 8, maxResults: 500 });
    expect(search.queries[0]?.method).toBeUndefined();
  });

  it('marks results as partial and lists unreadable subtrees', async () => {
    const search = scriptedSearch([
      { type: 'match', entry: entry({ name: 'app.log', path: '/srv/logs/app.log' }), parentPath: '/srv/logs' },
      { type: 'warning', path: '/srv/private', code: 'PermissionDenied', message: 'Permission denied: /srv/private' },
      { type: 'completed', summary: summary({ outcome: 'ResultLimitReached', matchCount: 1, warningCount: 1, isPartial: true }) },
    ]);
    render(<Harness source={makeSource()} search={search} />);

    const user = await openSearchAndRun('log');

    expect(await screen.findByRole('status')).toHaveTextContent('Partial: 1 match. Stopped at the result limit.');
    await user.click(screen.getByText('1 location could not be read'));
    expect(screen.getByText('Permission denied: /srv/private')).toBeInTheDocument();
  });

  it('shows the fallback notice and sends the chosen remote method', async () => {
    const search = scriptedSearch(
      [
        { type: 'notice', message: "Remote find is not used: 'find' is not installed on the server. Searching over SFTP instead." },
        { type: 'completed', summary: summary({}) },
      ],
      true,
    );
    const user = userEvent.setup();
    render(<Harness source={makeSource()} search={search} />);
    await screen.findByText('logs');
    await user.click(screen.getByRole('button', { name: 'Search files' }));

    await user.selectOptions(screen.getByLabelText('Method'), 'find');
    await user.type(screen.getByLabelText('Name pattern'), 'x');
    await user.click(screen.getByRole('button', { name: 'Search' }));

    expect(await screen.findByText(/Searching over SFTP instead/)).toBeInTheDocument();
    expect(search.queries[0]?.method).toBe('find');
    expect(screen.getByText('No matching names.')).toBeInTheDocument();
  });

  it('opens the location of a result and selects the entry', async () => {
    const search = scriptedSearch([
      { type: 'match', entry: entry({ name: 'db.log', path: '/srv/logs/db.log' }), parentPath: '/srv/logs' },
      { type: 'completed', summary: summary({ matchCount: 1 }) },
    ]);
    render(<Harness source={makeSource()} search={search} />);

    const user = await openSearchAndRun('db');
    await user.click(await screen.findByRole('button', { name: 'Open location of db.log' }));

    const row = (await screen.findByText('db.log')).closest('tr');
    expect(row).toHaveAttribute('aria-selected', 'true');
    expect(screen.queryByRole('search')).not.toBeInTheDocument();
  });

  it('stops a running search by aborting it', async () => {
    let captured: AbortSignal | null = null;
    const search: FileSearchSource = {
      supportsRemoteFind: false,
      search: vi.fn((_query: FileSearchQuery, onEvent: (event: FileSearchEvent) => void, signal: AbortSignal) => {
        captured = signal;
        onEvent({ type: 'match', entry: entry({ name: 'a.log', path: '/srv/a.log' }), parentPath: '/srv' });
        return new Promise<void>((_resolve, reject) => {
          signal.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError')));
        });
      }),
    };
    render(<Harness source={makeSource()} search={search} />);

    const user = await openSearchAndRun('a');
    expect(await screen.findByRole('status')).toHaveTextContent('Searching... 1 found');
    await user.click(screen.getByRole('button', { name: 'Stop' }));

    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('Stopped. 1 found, results are partial.'));
    expect((captured as AbortSignal | null)?.aborted).toBe(true);
  });

  it('shows the server error when the search is rejected', async () => {
    const search: FileSearchSource = {
      supportsRemoteFind: false,
      search: vi.fn().mockRejectedValue(new ApiError(400, { detail: 'Maximum depth must be between 1 and 64.' })),
    };
    render(<Harness source={makeSource()} search={search} />);

    await openSearchAndRun('x');

    expect(await screen.findByRole('status')).toHaveTextContent('Failed: Maximum depth must be between 1 and 64.');
  });
});

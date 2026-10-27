import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../api/client';
import type { DirectoryListing, FileEntry, FileSource } from '../api/files';
import { FilePane } from './FilePane';

function entry(overrides: Partial<FileEntry>): FileEntry {
  return {
    name: 'file.txt',
    path: '/home/file.txt',
    kind: 'File',
    size: 10,
    modifiedUtc: '2026-10-26T10:00:00Z',
    permissions: 'rw-r--r--',
    linkTargetKind: null,
    isBrokenLink: false,
    ...overrides,
  };
}

const HOME_LISTING: DirectoryListing = {
  path: '/home',
  parentPath: '/',
  entries: [
    entry({ name: 'zeta.txt', path: '/home/zeta.txt', size: 1 }),
    entry({ name: 'docs', path: '/home/docs', kind: 'Directory', size: 0 }),
    entry({ name: 'alpha.txt', path: '/home/alpha.txt', size: 99 }),
  ],
};

const DOCS_LISTING: DirectoryListing = {
  path: '/home/docs',
  parentPath: '/home',
  entries: [entry({ name: 'readme.md', path: '/home/docs/readme.md' })],
};

function makeSource(): FileSource {
  return {
    list: vi.fn((path: string | null) => {
      if (path === '/home/docs') {
        return Promise.resolve(DOCS_LISTING);
      }
      if (path === '/root') {
        return Promise.reject(new ApiError(422, { detail: 'Permission denied: /root' }));
      }
      return Promise.resolve(HOME_LISTING);
    }),
    createDirectory: vi.fn().mockResolvedValue(undefined),
    rename: vi.fn().mockResolvedValue(undefined),
    remove: vi.fn().mockResolvedValue(undefined),
  };
}

function Harness({ source, initialPath }: { source: FileSource; initialPath: string | null }) {
  const [path, setPath] = useState<string | null>(initialPath);
  return (
    <FilePane
      title="Test"
      sourceKey="test"
      source={source}
      path={path}
      onPathChange={setPath}
      roots={[{ name: 'Root', path: '/root' }]}
    />
  );
}

function renderedNames(): string[] {
  const rows = screen.getAllByRole('row').slice(1);
  return rows.map((row) => within(row).getAllByRole('cell')[0]?.textContent ?? '').filter((name) => name !== '..');
}

describe('FilePane', () => {
  let source: FileSource;

  beforeEach(() => {
    source = makeSource();
  });

  it('lists the default location and reports the resolved path', async () => {
    render(<Harness source={source} initialPath={null} />);

    expect(await screen.findByText('alpha.txt')).toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: 'Breadcrumbs' })).toHaveTextContent('home');
  });

  it('shows directories first, sorted by name', async () => {
    render(<Harness source={source} initialPath="/home" />);
    await screen.findByText('alpha.txt');

    expect(renderedNames()).toEqual(['docs', 'alpha.txt', 'zeta.txt']);
  });

  it('reverses the order when the active sort header is clicked', async () => {
    const user = userEvent.setup();
    render(<Harness source={source} initialPath="/home" />);
    await screen.findByText('alpha.txt');

    await user.click(screen.getByRole('button', { name: /^Name/ }));

    expect(renderedNames()).toEqual(['docs', 'zeta.txt', 'alpha.txt']);
  });

  it('navigates into a directory on double click and back to the parent folder', async () => {
    const user = userEvent.setup();
    render(<Harness source={source} initialPath="/home" />);

    await user.dblClick(await screen.findByText('docs'));
    expect(await screen.findByText('readme.md')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Parent folder' }));
    expect(await screen.findByText('alpha.txt')).toBeInTheDocument();
  });

  it('navigates through breadcrumbs', async () => {
    const user = userEvent.setup();
    render(<Harness source={source} initialPath="/home/docs" />);
    await screen.findByText('readme.md');

    const breadcrumbs = screen.getByRole('navigation', { name: 'Breadcrumbs' });
    await user.click(within(breadcrumbs).getByRole('button', { name: 'home' }));

    expect(await screen.findByText('alpha.txt')).toBeInTheDocument();
  });

  it('displays the server error when listing fails', async () => {
    const user = userEvent.setup();
    render(<Harness source={source} initialPath="/home" />);
    await screen.findByText('alpha.txt');

    await user.selectOptions(screen.getByLabelText('Test roots'), '/root');

    expect(await screen.findByRole('status')).toHaveTextContent('Permission denied: /root');
    expect(screen.queryByText('alpha.txt')).not.toBeInTheDocument();
  });

  it('selects multiple rows with ctrl click', async () => {
    const user = userEvent.setup();
    render(<Harness source={source} initialPath="/home" />);
    await screen.findByText('alpha.txt');

    await user.click(screen.getByText('alpha.txt'));
    await user.keyboard('{Control>}');
    await user.click(screen.getByText('zeta.txt'));
    await user.keyboard('{/Control}');

    const selected = screen.getAllByRole('row').filter((row) => row.getAttribute('aria-selected') === 'true');
    expect(selected).toHaveLength(2);
    expect(screen.getByRole('button', { name: 'Rename' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Delete' })).toBeEnabled();
  });

  it('does not delete until the confirmation is accepted', async () => {
    const user = userEvent.setup();
    render(<Harness source={source} initialPath="/home" />);

    await user.click(await screen.findByText('alpha.txt'));
    await user.click(screen.getByRole('button', { name: 'Delete' }));

    expect(source.remove).not.toHaveBeenCalled();
    const dialog = screen.getByRole('alertdialog', { name: 'Confirm delete' });
    await user.click(within(dialog).getByRole('button', { name: 'Confirm delete' }));

    await waitFor(() => expect(source.remove).toHaveBeenCalledWith('/home/alpha.txt'));
  });

  it('names the server and full paths in the delete confirmation', async () => {
    const user = userEvent.setup();
    render(<FilePane title="Remote" label="web-01" sourceKey="remote" source={source} path="/home" onPathChange={vi.fn()} />);

    await user.click(await screen.findByText('alpha.txt'));
    await user.click(screen.getByRole('button', { name: 'Delete' }));

    const dialog = screen.getByRole('alertdialog', { name: 'Confirm delete' });
    expect(dialog).toHaveTextContent('Delete 1 item(s) on web-01?');
    expect(within(dialog).getByText('/home/alpha.txt')).toBeInTheDocument();
  });

  it('disables every write action on a read-only server', async () => {
    const user = userEvent.setup();
    render(<FilePane title="Remote" sourceKey="remote" source={source} path="/home" onPathChange={vi.fn()} readOnly />);

    await user.click(await screen.findByText('alpha.txt'));

    expect(screen.getByRole('button', { name: 'New folder' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Rename' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Delete' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Delete' })).toHaveAttribute('title', 'Read-only server');
  });

  it('cancels a delete without calling the server', async () => {
    const user = userEvent.setup();
    render(<Harness source={source} initialPath="/home" />);

    await user.click(await screen.findByText('alpha.txt'));
    await user.click(screen.getByRole('button', { name: 'Delete' }));
    await user.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(source.remove).not.toHaveBeenCalled();
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  });

  it('shows the server error when a delete fails', async () => {
    const user = userEvent.setup();
    vi.mocked(source.remove).mockRejectedValue(new ApiError(409, { detail: 'Directory is not empty: /home/docs' }));
    render(<Harness source={source} initialPath="/home" />);

    await user.click(await screen.findByText('docs'));
    await user.click(screen.getByRole('button', { name: 'Delete' }));
    await user.click(screen.getByRole('button', { name: 'Confirm delete' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Directory is not empty');
  });

  it('creates a folder in the current directory', async () => {
    const user = userEvent.setup();
    render(<Harness source={source} initialPath="/home" />);
    await screen.findByText('alpha.txt');

    await user.click(screen.getByRole('button', { name: 'New folder' }));
    await user.type(screen.getByLabelText('Folder name'), 'projects');
    await user.click(screen.getByRole('button', { name: 'Create' }));

    await waitFor(() => expect(source.createDirectory).toHaveBeenCalledWith('/home', 'projects'));
  });

  it('renames the selected entry', async () => {
    const user = userEvent.setup();
    render(<Harness source={source} initialPath="/home" />);

    await user.click(await screen.findByText('alpha.txt'));
    await user.click(screen.getByRole('button', { name: 'Rename' }));
    const input = screen.getByLabelText('New name');
    await user.clear(input);
    await user.type(input, 'beta.txt');
    await user.click(within(screen.getByRole('form', { name: 'Rename entry' })).getByRole('button', { name: 'Rename' }));

    await waitFor(() => expect(source.rename).toHaveBeenCalledWith('/home/alpha.txt', 'beta.txt'));
  });

  it('shows a message for an empty folder', async () => {
    vi.mocked(source.list).mockResolvedValue({ path: '/empty', parentPath: '/', entries: [] });
    render(<Harness source={source} initialPath="/empty" />);

    expect(await screen.findByText('This folder is empty.')).toBeInTheDocument();
  });
});

import { createEvent, fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { DirectoryListing, FileEntry, FileSource } from '../api/files';
import { FilePane } from './FilePane';
import { dragMimeType, type FilePaneTransfer } from './filePaneTransfer';
import { ArrowRightIcon } from './icons/Icons';

function entry(name: string): FileEntry {
  return {
    name,
    path: `/home/${name}`,
    kind: 'File',
    size: 10,
    modifiedUtc: '2026-10-26T10:00:00Z',
    permissions: 'rw-r--r--',
    linkTargetKind: null,
    isBrokenLink: false,
  };
}

const LISTING: DirectoryListing = {
  path: '/home',
  parentPath: '/',
  entries: [entry('alpha.txt'), entry('beta.txt')],
};

function makeSource(): FileSource {
  return {
    list: vi.fn().mockResolvedValue(LISTING),
    createDirectory: vi.fn().mockResolvedValue(undefined),
    rename: vi.fn().mockResolvedValue(undefined),
    remove: vi.fn().mockResolvedValue(undefined),
  };
}

function makeTransfer(overrides: Partial<FilePaneTransfer> = {}): FilePaneTransfer {
  return {
    label: 'Upload',
    primary: true,
    Icon: ArrowRightIcon,
    canSend: true,
    unavailableReason: 'Open a remote folder to upload into.',
    onSend: vi.fn(),
    dragOrigin: 'local',
    acceptsFrom: 'remote',
    canReceive: true,
    onReceive: vi.fn(),
    ...overrides,
  };
}

function renderPane(transfer: FilePaneTransfer) {
  const source = makeSource();
  render(
    <FilePane title="Local" sourceKey="local" source={source} path="/home" onPathChange={vi.fn()} transfer={transfer} />,
  );
}

class FakeDataTransfer {
  private readonly data = new Map<string, string>();
  effectAllowed = 'none';
  dropEffect = 'none';

  get types(): string[] {
    return Array.from(this.data.keys());
  }

  setData(type: string, value: string): void {
    this.data.set(type, value);
  }

  getData(type: string): string {
    return this.data.get(type) ?? '';
  }
}

describe('FilePane transfers', () => {
  it('sends the selected entries', async () => {
    const transfer = makeTransfer();
    const user = userEvent.setup();
    renderPane(transfer);

    await user.click(await screen.findByText('alpha.txt'));
    await user.keyboard('{Control>}');
    await user.click(screen.getByText('beta.txt'));
    await user.keyboard('{/Control}');
    await user.click(screen.getByRole('button', { name: 'Upload' }));

    expect(transfer.onSend).toHaveBeenCalledWith(['/home/alpha.txt', '/home/beta.txt']);
  });

  it('disables sending without a selection or a target', async () => {
    const user = userEvent.setup();
    renderPane(makeTransfer({ canSend: false }));

    expect(screen.getByRole('button', { name: 'Upload' })).toBeDisabled();
    await user.click(await screen.findByText('alpha.txt'));

    expect(screen.getByRole('button', { name: 'Upload' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Upload' })).toHaveAttribute('title', 'Open a remote folder to upload into.');
  });

  it('writes the dragged row into the drag payload', async () => {
    renderPane(makeTransfer());
    const row = (await screen.findByText('alpha.txt')).closest('tr');
    const dataTransfer = new FakeDataTransfer();

    fireEvent.dragStart(row as HTMLElement, { dataTransfer });

    expect(JSON.parse(dataTransfer.getData(dragMimeType('local')))).toEqual({
      origin: 'local',
      paths: ['/home/alpha.txt'],
    });
  });

  it('receives paths dropped from the other pane', async () => {
    const transfer = makeTransfer();
    renderPane(transfer);
    await screen.findByText('alpha.txt');
    const pane = screen.getByRole('region', { name: 'Local files' });
    const dataTransfer = new FakeDataTransfer();
    dataTransfer.setData(dragMimeType('remote'), JSON.stringify({ origin: 'remote', paths: ['/srv/a.log'] }));

    const dragOver = createEvent.dragOver(pane, { dataTransfer });
    fireEvent(pane, dragOver);
    fireEvent.drop(pane, { dataTransfer });

    expect(dragOver.defaultPrevented).toBe(true);
    expect(transfer.onReceive).toHaveBeenCalledWith(['/srv/a.log']);
  });

  it('ignores drops from its own pane', async () => {
    const transfer = makeTransfer();
    renderPane(transfer);
    await screen.findByText('alpha.txt');
    const pane = screen.getByRole('region', { name: 'Local files' });
    const dataTransfer = new FakeDataTransfer();
    dataTransfer.setData(dragMimeType('local'), JSON.stringify({ origin: 'local', paths: ['/home/alpha.txt'] }));

    fireEvent.drop(pane, { dataTransfer });

    expect(transfer.onReceive).not.toHaveBeenCalled();
  });
});

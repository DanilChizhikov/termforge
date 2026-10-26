import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { EditorDocument, SaveConflict, SavedFile } from '../../api/editor';
import { openEditorFile, resetEditorFiles } from '../../state/editorFiles';
import { EditorWorkspace } from './EditorWorkspace';

vi.mock('./CodeEditor', () => ({
  default: ({ value, label, onChange }: { value: string; label: string; onChange: (text: string) => void }) => (
    <textarea aria-label={label} value={value} onChange={(event) => onChange(event.target.value)} />
  ),
}));

vi.mock('./CodeDiff', () => ({
  default: ({ original, modified }: { original: string; modified: string }) => (
    <div data-testid="diff">
      <pre data-testid="diff-original">{original}</pre>
      <pre data-testid="diff-modified">{modified}</pre>
    </div>
  ),
}));

const CONNECTION_ID = 'server-1';
const PATH = '/etc/app/app.yml';
const OPENED_VERSION = { modifiedUtc: '2026-10-26T10:00:00Z', size: 10, sha256: 'a'.repeat(64) };
const SERVER_VERSION = { modifiedUtc: '2026-10-26T10:05:00Z', size: 14, sha256: 'b'.repeat(64) };
const SAVED_VERSION = { modifiedUtc: '2026-10-26T10:06:00Z', size: 11, sha256: 'c'.repeat(64) };

const DOCUMENT: EditorDocument = {
  path: PATH,
  content: 'name: app\n',
  hasByteOrderMark: false,
  version: OPENED_VERSION,
  permissions: 'rw-r--r--',
};

const CONFLICT: SaveConflict = {
  code: 'Conflict',
  detail: 'The file changed on the server after you opened it.',
  currentVersion: SERVER_VERSION,
  currentContent: 'name: server\n',
  currentHasByteOrderMark: false,
  operationId: 'op-conflict',
};

function savedFile(version = SAVED_VERSION): SavedFile {
  return {
    version,
    backupName: '.app.yml.termforge-20261026T100600000Z.bak',
    strategy: 'AtomicRename',
    warnings: [],
    operationId: 'op-save',
  };
}

interface Recorded {
  method: string;
  url: string;
  body: unknown;
}

let fetchMock: ReturnType<typeof vi.fn>;
let saveResponses: Response[];
let requests: Recorded[];

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status });
}

beforeEach(() => {
  resetEditorFiles();
  requests = [];
  saveResponses = [];
  fetchMock = vi.fn((input: string, init?: RequestInit) => {
    const method = init?.method ?? 'GET';
    const url = String(input);
    const body = typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined;
    requests.push({ method, url, body });
    if (url === '/api/session') {
      return Promise.resolve(json({ csrfToken: 'token' }));
    }
    if (url.includes('/backups')) {
      return Promise.resolve(json([]));
    }
    if (method === 'PUT') {
      return Promise.resolve(saveResponses.shift() ?? json(savedFile()));
    }
    return Promise.resolve(json(DOCUMENT));
  });
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

function saves(): Recorded[] {
  return requests.filter((request) => request.method === 'PUT');
}

async function openAndEdit(text: string) {
  const user = userEvent.setup();
  render(<EditorWorkspace />);
  act(() => openEditorFile(CONNECTION_ID, PATH));
  const editor = await screen.findByLabelText(`Editing ${PATH}`);
  await user.clear(editor);
  await user.type(editor, text);
  return { user, editor };
}

describe('EditorWorkspace', () => {
  it('shows an empty state when nothing is open', () => {
    render(<EditorWorkspace />);

    expect(screen.getByText(/No files open/)).toBeInTheDocument();
  });

  it('opens the file with its path and permissions and starts clean', async () => {
    render(<EditorWorkspace />);
    act(() => openEditorFile(CONNECTION_ID, PATH));

    expect(await screen.findByLabelText(`Editing ${PATH}`)).toHaveValue('name: app\n');
    expect(screen.getByText(PATH, { selector: 'code' })).toBeInTheDocument();
    expect(screen.getByText('rw-r--r--')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^Save/ })).toBeDisabled();
    expect(screen.queryByText('(unsaved)')).not.toBeInTheDocument();
  });

  it('marks the tab dirty on edit and clean again after saving with the opened version', async () => {
    const { user } = await openAndEdit('name: mine');

    expect(screen.getByText('(unsaved)')).toBeInTheDocument();
    expect(screen.getByText('Unsaved changes')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /^Save/ }));

    await waitFor(() => expect(screen.queryByText('(unsaved)')).not.toBeInTheDocument());
    const [save] = saves();
    expect(save?.url).toBe(`/api/editor/remote/${CONNECTION_ID}/file`);
    expect(save?.body).toEqual({ path: PATH, content: 'name: mine', hasByteOrderMark: false, expectedVersion: OPENED_VERSION });
  });

  it('saves with the keyboard shortcut', async () => {
    const { user } = await openAndEdit('name: shortcut');

    await user.keyboard('{Control>}s{/Control}');

    await waitFor(() => expect(saves()).toHaveLength(1));
  });

  it('becomes clean again when the edit is undone by typing the original text', async () => {
    const { user, editor } = await openAndEdit('x');

    await user.clear(editor);
    await user.type(editor, 'name: app{Enter}');

    expect(screen.queryByText('(unsaved)')).not.toBeInTheDocument();
  });

  it('asks before closing a tab with unsaved changes', async () => {
    const { user } = await openAndEdit('name: mine');

    await user.click(screen.getByRole('button', { name: 'Close app.yml' }));
    const dialog = screen.getByRole('alertdialog', { name: 'Discard unsaved changes' });
    await user.click(within(dialog).getByRole('button', { name: 'Keep editing' }));

    expect(screen.getByLabelText(`Editing ${PATH}`)).toHaveValue('name: mine');
    await user.click(screen.getByRole('button', { name: 'Close app.yml' }));
    await user.click(screen.getByRole('button', { name: 'Discard and close' }));
    expect(screen.getByText(/No files open/)).toBeInTheDocument();
  });

  it('shows the conflict and never overwrites without an explicit confirmation', async () => {
    saveResponses.push(json(CONFLICT, 409));
    const { user } = await openAndEdit('name: mine');

    await user.click(screen.getByRole('button', { name: /^Save/ }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('The file changed on the server after you opened it.');
    expect(alert).toHaveTextContent('14 bytes');
    expect(screen.getByRole('button', { name: /^Save/ })).toBeDisabled();
    await user.click(within(alert).getByRole('button', { name: 'Overwrite…' }));
    expect(saves()).toHaveLength(1);
    await user.click(within(alert).getByRole('button', { name: 'Cancel' }));
    await user.click(within(alert).getByRole('button', { name: 'Overwrite…' }));
    await user.click(within(alert).getByRole('button', { name: 'Overwrite server copy' }));

    await waitFor(() => expect(saves()).toHaveLength(2));
    expect(saves()[1]?.body).toMatchObject({ content: 'name: mine', expectedVersion: SERVER_VERSION });
    await waitFor(() => expect(screen.queryByRole('alert')).not.toBeInTheDocument());
  });

  it('compares with the server copy and reloads it on request', async () => {
    saveResponses.push(json(CONFLICT, 409));
    const { user } = await openAndEdit('name: mine');
    await user.click(screen.getByRole('button', { name: /^Save/ }));
    const alert = await screen.findByRole('alert');

    await user.click(within(alert).getByRole('button', { name: 'Compare' }));
    expect(await screen.findByTestId('diff-original')).toHaveTextContent('name: server');
    expect(screen.getByTestId('diff-modified')).toHaveTextContent('name: mine');
    await user.click(within(alert).getByRole('button', { name: 'Reload from server' }));
    await user.click(screen.getByRole('button', { name: 'Edit' }));

    expect(await screen.findByLabelText(`Editing ${PATH}`)).toHaveValue('name: server\n');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.queryByText('(unsaved)')).not.toBeInTheDocument();
    expect(saves()).toHaveLength(1);
  });

  it('reports a failed save and keeps the edit', async () => {
    saveResponses.push(json({ code: 'PermissionDenied', detail: 'Saving failed and the original file was not changed.' }, 422));
    const { user } = await openAndEdit('name: mine');

    await user.click(screen.getByRole('button', { name: /^Save/ }));

    expect(await screen.findByRole('alert')).toHaveTextContent('original file was not changed');
    expect(screen.getByText('(unsaved)')).toBeInTheDocument();
  });

  it('shows why a file cannot be opened', async () => {
    fetchMock.mockImplementationOnce(() =>
      Promise.resolve(json({ code: 'Binary', detail: '/bin/ls looks like a binary file and cannot be edited as text.' }, 415)),
    );
    render(<EditorWorkspace />);

    act(() => openEditorFile(CONNECTION_ID, '/bin/ls'));

    expect(await screen.findByRole('alert')).toHaveTextContent('binary file');
  });
});

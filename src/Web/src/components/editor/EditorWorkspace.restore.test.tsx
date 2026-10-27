import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { EditorDocument, FileVersion } from '../../api/editor';
import { getEditorDraft, resetEditorDrafts, restoreEditorDrafts } from '../../state/editorDrafts';
import { editorFileKey, resetEditorFiles, restoreEditorFiles } from '../../state/editorFiles';
import { EditorHarness } from '../../test/EditorHarness';

vi.mock('./CodeEditor', () => ({
  default: ({ value, label, onChange }: { value: string; label: string; onChange: (text: string) => void }) => (
    <textarea aria-label={label} value={value} onChange={(event) => onChange(event.target.value)} />
  ),
}));

const CONNECTION_ID = 'server-1';
const PATH = '/etc/app/app.yml';
const KEY = editorFileKey(CONNECTION_ID, PATH);
const OPENED_VERSION: FileVersion = { modifiedUtc: '2026-10-26T10:00:00Z', size: 10, sha256: 'a'.repeat(64) };
const CHANGED_VERSION: FileVersion = { modifiedUtc: '2026-10-27T09:00:00Z', size: 12, sha256: 'b'.repeat(64) };

interface Recorded {
  method: string;
  url: string;
  body: unknown;
}

let requests: Recorded[];
let serverDocument: EditorDocument;

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status });
}

function restoreFileWithDraft(content: string | null): void {
  restoreEditorFiles([{ connectionId: CONNECTION_ID, path: PATH, dirty: content !== null }], KEY);
  restoreEditorDrafts(
    content === null ? [] : [{ connectionId: CONNECTION_ID, path: PATH, content, baseVersion: OPENED_VERSION }],
  );
}

beforeEach(() => {
  resetEditorFiles();
  resetEditorDrafts();
  requests = [];
  serverDocument = {
    path: PATH,
    content: 'name: app\n',
    hasByteOrderMark: false,
    version: OPENED_VERSION,
    permissions: 'rw-r--r--',
  };
  const fetchMock = vi.fn((input: string, init?: RequestInit) => {
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
      return Promise.resolve(
        json({ version: CHANGED_VERSION, backupName: null, strategy: 'AtomicRename', warnings: [], operationId: 'op' }),
      );
    }
    return Promise.resolve(json(serverDocument));
  });
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('EditorWorkspace restore', () => {
  it('shows a restored tab without reading the file until it is opened', () => {
    restoreFileWithDraft('name: mine\n');

    render(<EditorHarness />);

    expect(screen.getByText(/Restored from your last session/)).toBeInTheDocument();
    expect(screen.getByText(/unsaved changes were kept/)).toBeInTheDocument();
    expect(screen.getByText('(unsaved)')).toBeInTheDocument();
    expect(requests).toHaveLength(0);
  });

  it('recovers the unsaved content after opening the restored file', async () => {
    restoreFileWithDraft('name: mine\n');
    const user = userEvent.setup();
    render(<EditorHarness />);

    await user.click(screen.getByRole('button', { name: 'Open file' }));

    expect(await screen.findByLabelText(`Editing ${PATH}`)).toHaveValue('name: mine\n');
    expect(screen.getByText('Recovered unsaved changes from your last session.')).toBeInTheDocument();
    expect(screen.queryByText(/changed on the server since then/)).not.toBeInTheDocument();
    expect(screen.getByText('Unsaved changes')).toBeInTheDocument();
  });

  it('saves recovered content against the version it was based on when the file changed meanwhile', async () => {
    serverDocument = { ...serverDocument, content: 'name: server\n', version: CHANGED_VERSION };
    restoreFileWithDraft('name: mine\n');
    const user = userEvent.setup();
    render(<EditorHarness />);

    await user.click(screen.getByRole('button', { name: 'Open file' }));
    await screen.findByLabelText(`Editing ${PATH}`);
    expect(screen.getByText(/changed on the server since then/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /^Save/ }));

    await waitFor(() => expect(requests.some((request) => request.method === 'PUT')).toBe(true));
    const save = requests.find((request) => request.method === 'PUT');
    expect(save?.body).toEqual({ path: PATH, content: 'name: mine\n', hasByteOrderMark: false, expectedVersion: OPENED_VERSION });
  });

  it('opens a restored file without draft as it is on the server', async () => {
    restoreFileWithDraft(null);
    const user = userEvent.setup();
    render(<EditorHarness />);

    await user.click(screen.getByRole('button', { name: 'Open file' }));

    expect(await screen.findByLabelText(`Editing ${PATH}`)).toHaveValue('name: app\n');
    expect(screen.queryByText(/Recovered unsaved changes/)).not.toBeInTheDocument();
  });

  it('keeps the draft while editing and drops it when the changes are discarded', async () => {
    restoreFileWithDraft('name: mine\n');
    const user = userEvent.setup();
    render(<EditorHarness />);
    await user.click(screen.getByRole('button', { name: 'Open file' }));
    const editor = await screen.findByLabelText(`Editing ${PATH}`);

    await user.type(editor, 'x');
    expect(getEditorDraft(KEY)?.content).toBe('name: mine\nx');
    await act(async () => {
      await user.click(screen.getByRole('button', { name: 'Discard changes' }));
    });

    expect(getEditorDraft(KEY)).toBeNull();
  });
});

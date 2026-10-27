import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { EditorDocument, SavedFile } from '../../api/editor';
import type { FileAccess, SudoStatus } from '../../api/sudoEditor';
import { resetEditorDrafts } from '../../state/editorDrafts';
import { openEditorFile, resetEditorFiles } from '../../state/editorFiles';
import { EditorHarness } from '../../test/EditorHarness';

vi.mock('./CodeEditor', () => ({
  default: ({ value, label, onChange }: { value: string; label: string; onChange: (text: string) => void }) => (
    <textarea aria-label={label} value={value} onChange={(event) => onChange(event.target.value)} />
  ),
}));

vi.mock('./CodeDiff', () => ({
  default: () => <div data-testid="diff" />,
}));

const CONNECTION_ID = 'server-1';
const PATH = '/etc/nginx/nginx.conf';
const SUDO_PATH = `/api/editor/remote/${CONNECTION_ID}/sudo`;
const PASSWORD = 'sudo-secret';
const VERSION = { modifiedUtc: '2026-10-26T10:00:00Z', size: 10, sha256: 'a'.repeat(64) };

const DOCUMENT: EditorDocument = {
  path: PATH,
  content: 'worker 1;\n',
  hasByteOrderMark: false,
  version: VERSION,
  permissions: 'rw-------',
  ownerId: 0,
};

const SAVED: SavedFile = {
  version: { ...VERSION, sha256: 'c'.repeat(64) },
  backupName: '.nginx.conf.termforge-20261026T100600000Z.bak',
  strategy: 'AtomicRename',
  warnings: [],
  operationId: 'op-save',
};

interface Recorded {
  method: string;
  url: string;
  body: unknown;
}

let requests: Recorded[];
let status: SudoStatus;
let access: FileAccess;
let openResponse: () => Response;
let sudoSaveResponses: Response[];

function json(body: unknown, statusCode = 200): Response {
  return new Response(JSON.stringify(body), { status: statusCode });
}

function sudoSaves(): Recorded[] {
  return requests.filter((request) => request.url === `${SUDO_PATH}/file`);
}

beforeEach(() => {
  resetEditorFiles();
  resetEditorDrafts();
  requests = [];
  sudoSaveResponses = [];
  status = { availability: 'PasswordRequired', hasSavedPassword: false, message: 'sudo needs a password.' };
  access = { ownerId: 0, ownerName: 'root', writable: false };
  openResponse = () => json(DOCUMENT);
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
    if (url.startsWith(`${SUDO_PATH}/access?`)) {
      return Promise.resolve(json(access));
    }
    if (url === `${SUDO_PATH}/status`) {
      return Promise.resolve(json(status));
    }
    if (url === `${SUDO_PATH}/file`) {
      return Promise.resolve(sudoSaveResponses.shift() ?? json(SAVED));
    }
    if (url === `${SUDO_PATH}/open`) {
      return Promise.resolve(json(DOCUMENT));
    }
    return Promise.resolve(openResponse());
  });
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

async function openAndEdit(text: string) {
  const user = userEvent.setup();
  render(<EditorHarness />);
  act(() => openEditorFile(CONNECTION_ID, PATH));
  const editor = await screen.findByLabelText(`Editing ${PATH}`);
  await user.clear(editor);
  await user.type(editor, text);
  return user;
}

describe('sudo editing', () => {
  it('switches to sudo editing and asks for the password when saving', async () => {
    const user = await openAndEdit('worker 2;');

    expect(await screen.findByText('owned by root · save needs sudo')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Edit with sudo' }));
    expect(await screen.findByText('owned by root · editing with sudo')).toBeInTheDocument();
    expect(screen.queryByText('owned by root · save needs sudo')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /^Save/ }));
    const dialog = await screen.findByRole('dialog', { name: 'Sudo password' });
    await user.type(within(dialog).getByLabelText('Password'), PASSWORD);
    await user.click(within(dialog).getByRole('button', { name: 'Continue with sudo' }));

    await waitFor(() => expect(screen.queryByText('(unsaved)')).not.toBeInTheDocument());
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    const [save] = sudoSaves();
    expect(save?.method).toBe('PUT');
    expect(save?.body).toEqual({
      path: PATH,
      content: 'worker 2;',
      hasByteOrderMark: false,
      expectedVersion: VERSION,
      sudo: { password: PASSWORD, rememberPassword: false },
    });
    const others = requests.filter((request) => request !== save);
    expect(JSON.stringify(others)).not.toContain(PASSWORD);
  });

  it('asks again after a wrong password and keeps the edit', async () => {
    sudoSaveResponses.push(json({ code: 'SudoPasswordIncorrect', detail: 'The sudo password is incorrect.' }, 422));
    const user = await openAndEdit('worker 3;');
    await user.click(screen.getByRole('button', { name: 'Edit with sudo' }));
    await screen.findByText('owned by root · editing with sudo');
    await user.click(screen.getByRole('button', { name: /^Save/ }));
    let dialog = await screen.findByRole('dialog', { name: 'Sudo password' });
    await user.type(within(dialog).getByLabelText('Password'), 'wrong');
    await user.click(within(dialog).getByRole('button', { name: 'Continue with sudo' }));

    dialog = await screen.findByRole('dialog', { name: 'Sudo password' });
    expect(await within(dialog).findByText(/incorrect/)).toBeInTheDocument();
    expect(screen.getByText('(unsaved)')).toBeInTheDocument();
    await user.type(within(dialog).getByLabelText('Password'), PASSWORD);
    await user.click(within(dialog).getByRole('switch', { name: 'Remember in credential store' }));
    await user.click(within(dialog).getByRole('button', { name: 'Continue with sudo' }));

    await waitFor(() => expect(sudoSaves()).toHaveLength(2));
    expect(sudoSaves()[1]?.body).toMatchObject({ sudo: { password: PASSWORD, rememberPassword: true } });
    await waitFor(() => expect(screen.queryByText('(unsaved)')).not.toBeInTheDocument());
  });

  it('does not save when the password prompt is cancelled', async () => {
    const user = await openAndEdit('worker 4;');
    await user.click(screen.getByRole('button', { name: 'Edit with sudo' }));
    await screen.findByText('owned by root · editing with sudo');

    await user.click(screen.getByRole('button', { name: /^Save/ }));
    const dialog = await screen.findByRole('dialog', { name: 'Sudo password' });
    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(sudoSaves()).toHaveLength(0);
    expect(screen.getByRole('button', { name: /^Save/ })).toBeEnabled();
  });

  it('offers to open a permission-denied file with sudo', async () => {
    status = { availability: 'Passwordless', hasSavedPassword: false, message: 'sudo works without a password.' };
    openResponse = () => json({ code: 'PermissionDenied', detail: `Permission denied: ${PATH}` }, 422);
    const user = userEvent.setup();
    render(<EditorHarness />);
    act(() => openEditorFile(CONNECTION_ID, PATH));

    await user.click(await screen.findByRole('button', { name: 'Open with sudo' }));

    expect(await screen.findByLabelText(`Editing ${PATH}`)).toHaveValue('worker 1;\n');
    expect(await screen.findByText('owned by root · editing with sudo')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    const open = requests.find((request) => request.url === `${SUDO_PATH}/open`);
    expect(open?.body).toEqual({ path: PATH, sudo: { password: null, rememberPassword: false } });
  });

  it('explains when sudo is unavailable and stays in normal mode', async () => {
    status = { availability: 'Unavailable', hasSavedPassword: false, message: 'This user is not allowed to use sudo on this server.' };
    const user = userEvent.setup();
    render(<EditorHarness />);
    act(() => openEditorFile(CONNECTION_ID, PATH));
    await screen.findByLabelText(`Editing ${PATH}`);

    await user.click(screen.getByRole('button', { name: 'Edit with sudo' }));

    expect(await screen.findByText('This user is not allowed to use sudo on this server.')).toBeInTheDocument();
    expect(screen.queryByText(/editing with sudo/)).not.toBeInTheDocument();
    expect(screen.getByText('owned by root · save needs sudo')).toBeInTheDocument();
  });

  it('forgets a remembered password', async () => {
    status = { availability: 'PasswordRequired', hasSavedPassword: true, message: 'sudo needs a password.' };
    const user = userEvent.setup();
    render(<EditorHarness />);
    act(() => openEditorFile(CONNECTION_ID, PATH));
    await screen.findByLabelText(`Editing ${PATH}`);
    await user.click(screen.getByRole('button', { name: 'Edit with sudo' }));

    await user.click(await screen.findByRole('button', { name: 'Forget sudo password' }));

    await waitFor(() => expect(requests.some((request) => request.method === 'DELETE' && request.url === `${SUDO_PATH}/password`)).toBe(true));
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Forget sudo password' })).not.toBeInTheDocument());
  });

  it('falls back to the uid when the owner name cannot be resolved', async () => {
    access = { ownerId: 0, ownerName: null, writable: false };
    render(<EditorHarness />);
    act(() => openEditorFile(CONNECTION_ID, PATH));

    expect(await screen.findByText('owned by uid 0 · save needs sudo')).toBeInTheDocument();
    const probe = requests.find((request) => request.url.startsWith(`${SUDO_PATH}/access?`));
    expect(probe?.url).toBe(`${SUDO_PATH}/access?path=${encodeURIComponent(PATH)}&ownerId=0`);
  });

  it('shows no sudo badge for a file the user can write', async () => {
    access = { ownerId: 1000, ownerName: 'deploy', writable: true };
    render(<EditorHarness />);
    act(() => openEditorFile(CONNECTION_ID, PATH));
    await screen.findByLabelText(`Editing ${PATH}`);

    await waitFor(() => expect(requests.some((request) => request.url.startsWith(`${SUDO_PATH}/access?`))).toBe(true));
    expect(screen.queryByText(/needs sudo/)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Edit with sudo' })).toBeInTheDocument();
  });
});

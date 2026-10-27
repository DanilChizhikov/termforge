import { lazy, Suspense, useCallback, useEffect, useReducer, useRef, useState, type KeyboardEvent } from 'react';
import {
  describeEditorError,
  openRemoteFile,
  readBackup,
  rollbackRemoteFile,
  saveRemoteFile,
  type EditorDocument,
  type FileBackup,
  type FileVersion,
  type SaveOutcome,
} from '../../api/editor';
import {
  getFileAccess,
  isPermissionDenied,
  openRemoteFileWithSudo,
  readBackupWithSudo,
  rollbackRemoteFileWithSudo,
  saveRemoteFileWithSudo,
  type FileAccess,
  type SudoCredentials,
} from '../../api/sudoEditor';
import {
  editorDocumentReducer,
  INITIAL_DOCUMENT_STATE,
  isDirty,
  lineEndingOf,
} from '../../state/editorDocument';
import { clearEditorDraft, getEditorDraft, setEditorDraft } from '../../state/editorDrafts';
import { reopenEditorFile, setEditorFileDirty, type EditorFile } from '../../state/editorFiles';
import { useConnectionName } from '../../state/connections';
import { EmptyState } from '../EmptyState';
import { ShieldIcon } from '../icons/Icons';
import { formatClock, formatDayAndClock } from '../timeFormat';
import { EditorConflictNotice } from './EditorConflictNotice';
import { languageForPath } from './editorLanguage';
import { EditorVersions } from './EditorVersions';
import { SudoAccessBadge } from './SudoAccessBadge';
import { SudoPasswordDialog } from './SudoPasswordDialog';
import { useSudoElevation } from './useSudoElevation';

const CodeEditor = lazy(() => import('./CodeEditor'));
const CodeDiff = lazy(() => import('./CodeDiff'));

const ELEVATED_SAVE_NOTE = 'Every step runs with sudo and keeps the owner, group and mode of the file.';

export interface EditorDocumentViewProps {
  file: EditorFile;
}

interface Comparison {
  title: string;
  content: string;
}

type View = 'edit' | 'diff';

interface Cursor {
  line: number;
  column: number;
}

export function EditorDocumentView({ file }: EditorDocumentViewProps) {
  if (file.restored) {
    return <RestoredEditorFile file={file} />;
  }
  return <OpenedEditorDocument file={file} />;
}

function RestoredEditorFile({ file }: EditorDocumentViewProps) {
  return (
    <div className="editor-restored">
      <p>
        Restored from your last session: <code className="mono">{file.path}</code>
      </p>
      {file.dirty ? <p>Your unsaved changes were kept and are applied when the file is opened again.</p> : null}
      <button type="button" className="btn btn-sm btn-primary" onClick={() => reopenEditorFile(file.key)}>
        Open file
      </button>
    </div>
  );
}

function OpenedEditorDocument({ file }: EditorDocumentViewProps) {
  const [state, dispatch] = useReducer(editorDocumentReducer, INITIAL_DOCUMENT_STATE);
  const pendingRecovery = useRef(getEditorDraft(file.key));
  const [view, setView] = useState<View>('edit');
  const [comparison, setComparison] = useState<Comparison | null>(null);
  const [cursor, setCursor] = useState<Cursor>({ line: 1, column: 1 });
  const [versionsToken, setVersionsToken] = useState(0);
  const [reloadToken, setReloadToken] = useState(0);
  const [elevated, setElevated] = useState(false);
  const [openDenied, setOpenDenied] = useState(false);
  const [openedAt] = useState(() => new Date());
  const serverName = useConnectionName(file.connectionId);
  const dirty = isDirty(state);
  const language = languageForPath(file.path);
  const { connectionId, path, key } = file;
  const sudo = useSudoElevation(connectionId);
  const [access, setAccess] = useState<FileAccess | null>(null);
  const accessRequest = useRef(0);

  const loadAccess = useCallback(
    (document: EditorDocument) => {
      const request = accessRequest.current + 1;
      accessRequest.current = request;
      setAccess(null);
      if (document.ownerId === undefined) {
        return;
      }
      getFileAccess(connectionId, path, document.ownerId)
        .then((loaded) => {
          if (accessRequest.current === request) {
            setAccess(loaded);
          }
        })
        .catch(() => undefined);
    },
    [connectionId, path],
  );

  useEffect(() => {
    let cancelled = false;
    openRemoteFile(connectionId, path)
      .then((document) => {
        if (!cancelled) {
          dispatch({ type: 'loaded', document });
          loadAccess(document);
          const recovered = pendingRecovery.current;
          pendingRecovery.current = null;
          if (recovered !== null && recovered.content !== document.content) {
            dispatch({ type: 'recovered', content: recovered.content, baseVersion: recovered.baseVersion });
          }
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setOpenDenied(isPermissionDenied(error));
          dispatch({ type: 'loadFailed', error: describeEditorError(error) });
        }
      });
    return () => {
      cancelled = true;
    };
  }, [connectionId, path, reloadToken, loadAccess]);

  useEffect(() => {
    setEditorFileDirty(key, dirty);
  }, [key, dirty]);

  const serverCopy = state.server;
  const draftContent = state.draft;
  useEffect(() => {
    if (serverCopy === null) {
      return;
    }
    if (dirty) {
      setEditorDraft(key, { connectionId, path, content: draftContent, baseVersion: serverCopy.version });
    } else {
      clearEditorDraft(key);
    }
  }, [key, connectionId, path, dirty, draftContent, serverCopy]);

  const handleCursor = useCallback((line: number, column: number) => setCursor({ line, column }), []);

  function applyOutcome(outcome: SaveOutcome, content: string): void {
    if (outcome.kind === 'conflict') {
      dispatch({ type: 'conflicted', conflict: outcome.conflict });
      return;
    }
    if (outcome.saved.document != null) {
      dispatch({ type: 'loaded', document: outcome.saved.document });
    } else {
      dispatch({ type: 'saved', saved: outcome.saved, content });
    }
    setComparison(null);
    setVersionsToken((token) => token + 1);
  }

  function failSave(error: unknown): void {
    dispatch({ type: 'saveFailed', error: describeEditorError(error) });
  }

  function saveElevated(label: string, content: string, send: (sudo: SudoCredentials) => Promise<SaveOutcome>): Promise<void> {
    return sudo.perform({
      label,
      run: async (credentials) => {
        dispatch({ type: 'saveStarted' });
        const outcome = await send(credentials);
        applyOutcome(outcome, content);
      },
      onError: failSave,
      onAbort: () => dispatch({ type: 'saveAborted' }),
    });
  }

  async function save(expectedVersion: FileVersion | null): Promise<void> {
    if (state.server === null) {
      return;
    }
    const content = state.draft;
    const request = { path, content, hasByteOrderMark: state.server.hasByteOrderMark, expectedVersion };
    if (elevated) {
      await saveElevated('Saving this file', content, (credentials) =>
        saveRemoteFileWithSudo(connectionId, request, credentials),
      );
      return;
    }
    dispatch({ type: 'saveStarted' });
    try {
      const outcome = await saveRemoteFile(connectionId, request);
      applyOutcome(outcome, content);
    } catch (error) {
      failSave(error);
    }
  }

  async function rollBack(backup: FileBackup): Promise<void> {
    if (state.server === null) {
      return;
    }
    const content = state.draft;
    const request = { path, backupName: backup.name, expectedVersion: state.server.version };
    if (elevated) {
      await saveElevated('Rolling back this file', content, (credentials) =>
        rollbackRemoteFileWithSudo(connectionId, request, credentials),
      );
      return;
    }
    dispatch({ type: 'saveStarted' });
    try {
      const outcome = await rollbackRemoteFile(connectionId, request);
      applyOutcome(outcome, content);
    } catch (error) {
      failSave(error);
    }
  }

  function showBackup(backup: FileBackup, content: string): void {
    setComparison({ title: `Backup · ${formatDayAndClock(backup.createdAtUtc)}`, content });
    setView('diff');
  }

  async function compareWithBackup(backup: FileBackup): Promise<void> {
    if (elevated) {
      await sudo.perform({
        label: 'Reading this backup',
        run: async (credentials) => {
          const document = await readBackupWithSudo(connectionId, path, backup.name, credentials);
          showBackup(backup, document.content);
        },
        onError: failSave,
      });
      return;
    }
    try {
      const document = await readBackup(connectionId, path, backup.name);
      showBackup(backup, document.content);
    } catch (error) {
      failSave(error);
    }
  }

  function openWithSudo(): void {
    void sudo.perform({
      label: 'Opening this file',
      run: async (credentials) => {
        const document = await openRemoteFileWithSudo(connectionId, path, credentials);
        setOpenDenied(false);
        setElevated(true);
        dispatch({ type: 'loaded', document });
        loadAccess(document);
        const recovered = pendingRecovery.current;
        pendingRecovery.current = null;
        if (recovered !== null && recovered.content !== document.content) {
          dispatch({ type: 'recovered', content: recovered.content, baseVersion: recovered.baseVersion });
        }
      },
      onError: (error) => dispatch({ type: 'loadFailed', error: describeEditorError(error) }),
    });
  }

  function editWithSudo(): void {
    void sudo.check().then((usable) => {
      if (usable) {
        setElevated(true);
      }
    });
  }

  function compareWithServer(): void {
    if (state.conflict?.currentContent == null) {
      return;
    }
    const changedAt = state.conflict.currentVersion === null ? '' : ` · changed ${formatClock(state.conflict.currentVersion.modifiedUtc)}`;
    setComparison({ title: `On server${changedAt}`, content: state.conflict.currentContent });
    setView('diff');
  }

  function requestSave(): void {
    if (dirty && !state.saving && state.conflict === null && state.server !== null) {
      void save(state.server.version);
    }
  }

  function handleKeyDown(event: KeyboardEvent<HTMLDivElement>): void {
    if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 's') {
      event.preventDefault();
      requestSave();
    }
  }

  const sudoNotice =
    sudo.notice === null ? null : (
      <p role="status" className="notice notice-caution editor-message">
        {sudo.notice}
      </p>
    );
  const sudoDialog =
    sudo.prompt === null ? null : (
      <SudoPasswordDialog
        actionLabel={sudo.prompt.action.label}
        problem={sudo.prompt.problem}
        busy={sudo.busy}
        onSubmit={(credentials) => void sudo.submit(credentials)}
        onCancel={sudo.cancel}
      />
    );

  if (state.status === 'loading') {
    return <EmptyState message={`Opening ${path}…`} />;
  }

  if (state.status === 'failed' || state.server === null) {
    return (
      <div className="editor-document editor-document-failed">
        <p role="alert" className="notice notice-danger">
          {state.error ?? 'The file could not be opened.'}
        </p>
        {sudoNotice}
        <div className="editor-failed-actions">
          <button type="button" className="btn btn-sm" onClick={() => setReloadToken((token) => token + 1)}>
            Try again
          </button>
          {openDenied ? (
            <button type="button" className="btn btn-sm" disabled={sudo.busy} onClick={openWithSudo}>
              <ShieldIcon size={13} />
              Open with sudo
            </button>
          ) : null}
        </div>
        {sudoDialog}
      </div>
    );
  }

  const server = state.server;
  const diffOriginal = comparison?.content ?? server.content;
  const diffTitle = comparison?.title ?? 'Opened version';

  return (
    <div className="editor-document" onKeyDown={handleKeyDown}>
      <div className="editor-toolbar">
        <code className="editor-path" title={path}>
          {serverName === null ? null : <span className="editor-path-server">{serverName}:</span>}
          {path}
        </code>
        <SudoAccessBadge access={access} elevated={elevated} />
        <span className="badge" title="Permissions">
          <span className="mono">{server.permissions}</span>
        </span>
        {dirty ? <span className="editor-unsaved">Unsaved changes</span> : null}
        <span className="editor-toolbar-spacer" />
        <div role="group" aria-label="View" className="segmented">
          <button type="button" aria-pressed={view === 'edit'} onClick={() => setView('edit')}>
            Edit
          </button>
          <button type="button" aria-pressed={view === 'diff'} onClick={() => setView('diff')}>
            Diff
          </button>
        </div>
        {elevated && sudo.status?.hasSavedPassword === true ? (
          <button type="button" className="btn btn-sm btn-ghost" onClick={() => void sudo.forget()}>
            Forget sudo password
          </button>
        ) : null}
        {elevated ? (
          <button type="button" className="btn btn-sm" disabled={state.saving} onClick={() => setElevated(false)}>
            Stop using sudo
          </button>
        ) : (
          <button type="button" className="btn btn-sm" disabled={state.saving || sudo.busy} onClick={editWithSudo}>
            <ShieldIcon size={13} />
            Edit with sudo
          </button>
        )}
        <button
          type="button"
          className="btn btn-sm"
          disabled={!dirty || state.saving}
          onClick={() => dispatch({ type: 'edited', text: server.content })}
        >
          Discard changes
        </button>
        <button
          type="button"
          className="btn btn-sm btn-primary editor-save"
          disabled={!dirty || state.saving || state.conflict !== null}
          onClick={requestSave}
        >
          {state.saving ? 'Saving…' : 'Save'} <kbd className="kbd">⌘S</kbd>
        </button>
      </div>
      {state.conflict === null ? null : (
        <EditorConflictNotice
          conflict={state.conflict}
          openedSize={server.version?.size ?? null}
          busy={state.saving}
          comparing={view === 'diff' && comparison?.content === state.conflict.currentContent}
          onCompare={compareWithServer}
          onReload={() => {
            setComparison(null);
            dispatch({ type: 'reloadedFromConflict' });
          }}
          onOverwrite={() => void save(state.conflict?.currentVersion ?? null)}
        />
      )}
      {state.recovery === 'none' ? null : (
        <div role="status" className="notice notice-caution editor-message">
          <p>Recovered unsaved changes from your last session.</p>
          {state.recovery === 'changed' ? (
            <p>The file changed on the server since then. Saving shows the conflict so you can compare first.</p>
          ) : null}
        </div>
      )}
      {sudoNotice}
      {state.error === null ? null : (
        <p role="alert" className="notice notice-danger editor-message">
          {state.error}
        </p>
      )}
      {state.warnings.length === 0 ? null : (
        <div role="status" className="notice notice-caution editor-message">
          {state.warnings.map((warning) => (
            <p key={warning}>{warning}</p>
          ))}
        </div>
      )}
      <div className="editor-body">
        <div className="editor-surface">
          {view === 'diff' ? (
            <div className="editor-diff-header">
              <span className="editor-diff-title">{diffTitle}</span>
              <span className="editor-diff-title">
                Your version
                {dirty ? <span className="editor-diff-note">unsaved changes</span> : null}
              </span>
            </div>
          ) : null}
          <Suspense fallback={<EmptyState message="Loading editor…" />}>
            {view === 'edit' ? (
              <CodeEditor
                value={state.draft}
                language={language.id}
                label={`Editing ${path}`}
                onChange={(text) => dispatch({ type: 'edited', text })}
                onCursorChange={handleCursor}
              />
            ) : (
              <CodeDiff original={diffOriginal} modified={state.draft} language={language.id} />
            )}
          </Suspense>
        </div>
        <EditorVersions
          connectionId={connectionId}
          path={path}
          refreshToken={versionsToken}
          canRollBack={!dirty && state.conflict === null}
          busy={state.saving}
          saveNote={elevated ? ELEVATED_SAVE_NOTE : null}
          onCompare={(backup) => void compareWithBackup(backup)}
          onRollBack={(backup) => void rollBack(backup)}
        />
      </div>
      <footer className="editor-footer">
        <span>
          Ln {cursor.line}, Col {cursor.column}
        </span>
        <span>{server.hasByteOrderMark ? 'UTF-8 with BOM' : 'UTF-8'}</span>
        <span>{lineEndingOf(state.draft)}</span>
        <span>{language.label}</span>
        <span className="editor-footer-spacer" />
        <span>Opened {formatClock(openedAt)}</span>
      </footer>
      {sudoDialog}
    </div>
  );
}

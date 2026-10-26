import { lazy, Suspense, useCallback, useEffect, useReducer, useState, type KeyboardEvent } from 'react';
import {
  describeEditorError,
  openRemoteFile,
  readBackup,
  rollbackRemoteFile,
  saveRemoteFile,
  type FileBackup,
  type FileVersion,
  type SaveOutcome,
} from '../../api/editor';
import {
  editorDocumentReducer,
  INITIAL_DOCUMENT_STATE,
  isDirty,
  lineEndingOf,
} from '../../state/editorDocument';
import { setEditorFileDirty, type EditorFile } from '../../state/editorFiles';
import { EmptyState } from '../EmptyState';
import { EditorConflictNotice } from './EditorConflictNotice';
import { languageForPath } from './editorLanguage';
import { EditorVersions } from './EditorVersions';

const CodeEditor = lazy(() => import('./CodeEditor'));
const CodeDiff = lazy(() => import('./CodeDiff'));

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
  const [state, dispatch] = useReducer(editorDocumentReducer, INITIAL_DOCUMENT_STATE);
  const [view, setView] = useState<View>('edit');
  const [comparison, setComparison] = useState<Comparison | null>(null);
  const [cursor, setCursor] = useState<Cursor>({ line: 1, column: 1 });
  const [versionsToken, setVersionsToken] = useState(0);
  const [reloadToken, setReloadToken] = useState(0);
  const dirty = isDirty(state);
  const language = languageForPath(file.path);
  const { connectionId, path, key } = file;

  useEffect(() => {
    let cancelled = false;
    openRemoteFile(connectionId, path)
      .then((document) => {
        if (!cancelled) {
          dispatch({ type: 'loaded', document });
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          dispatch({ type: 'loadFailed', error: describeEditorError(error) });
        }
      });
    return () => {
      cancelled = true;
    };
  }, [connectionId, path, reloadToken]);

  useEffect(() => {
    setEditorFileDirty(key, dirty);
  }, [key, dirty]);

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

  async function save(expectedVersion: FileVersion | null): Promise<void> {
    if (state.server === null) {
      return;
    }
    const content = state.draft;
    dispatch({ type: 'saveStarted' });
    try {
      const request = { path, content, hasByteOrderMark: state.server.hasByteOrderMark, expectedVersion };
      const outcome = await saveRemoteFile(connectionId, request);
      applyOutcome(outcome, content);
    } catch (error) {
      dispatch({ type: 'saveFailed', error: describeEditorError(error) });
    }
  }

  async function rollBack(backup: FileBackup): Promise<void> {
    if (state.server === null) {
      return;
    }
    dispatch({ type: 'saveStarted' });
    try {
      const request = { path, backupName: backup.name, expectedVersion: state.server.version };
      const outcome = await rollbackRemoteFile(connectionId, request);
      applyOutcome(outcome, state.draft);
    } catch (error) {
      dispatch({ type: 'saveFailed', error: describeEditorError(error) });
    }
  }

  async function compareWithBackup(backup: FileBackup): Promise<void> {
    try {
      const document = await readBackup(connectionId, path, backup.name);
      setComparison({ title: `Backup ${new Date(backup.createdAtUtc).toLocaleString()}`, content: document.content });
      setView('diff');
    } catch (error) {
      dispatch({ type: 'saveFailed', error: describeEditorError(error) });
    }
  }

  function compareWithServer(): void {
    if (state.conflict?.currentContent == null) {
      return;
    }
    setComparison({ title: 'On server now', content: state.conflict.currentContent });
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

  if (state.status === 'loading') {
    return <EmptyState message={`Opening ${path}…`} />;
  }

  if (state.status === 'failed' || state.server === null) {
    return (
      <div className="editor-document editor-document-failed">
        <p role="alert" className="notice notice-danger">
          {state.error ?? 'The file could not be opened.'}
        </p>
        <button type="button" className="btn btn-sm" onClick={() => setReloadToken((token) => token + 1)}>
          Try again
        </button>
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
          {path}
        </code>
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
          className="btn btn-sm btn-primary"
          disabled={!dirty || state.saving || state.conflict !== null}
          onClick={requestSave}
        >
          {state.saving ? 'Saving…' : 'Save'} <kbd className="kbd">⌘S</kbd>
        </button>
      </div>
      {state.conflict === null ? null : (
        <EditorConflictNotice
          conflict={state.conflict}
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
              <span>{diffTitle}</span>
              <span>Your version</span>
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
      </footer>
    </div>
  );
}

import type { EditorDocument, FileVersion, SaveConflict, SavedFile } from '../api/editor';

export interface ServerCopy {
  content: string;
  hasByteOrderMark: boolean;
  version: FileVersion | null;
  permissions: string;
}

export type DraftRecovery = 'none' | 'unchanged' | 'changed';

export interface EditorDocumentState {
  status: 'loading' | 'ready' | 'failed';
  error: string | null;
  server: ServerCopy | null;
  draft: string;
  conflict: SaveConflict | null;
  saving: boolean;
  warnings: string[];
  recovery: DraftRecovery;
}

export type EditorDocumentAction =
  | { type: 'loaded'; document: EditorDocument }
  | { type: 'loadFailed'; error: string }
  | { type: 'edited'; text: string }
  | { type: 'saveStarted' }
  | { type: 'saved'; saved: SavedFile; content: string }
  | { type: 'conflicted'; conflict: SaveConflict }
  | { type: 'saveFailed'; error: string }
  | { type: 'saveAborted' }
  | { type: 'reloadedFromConflict' }
  | { type: 'recovered'; content: string; baseVersion: FileVersion | null };

export const INITIAL_DOCUMENT_STATE: EditorDocumentState = {
  status: 'loading',
  error: null,
  server: null,
  draft: '',
  conflict: null,
  saving: false,
  warnings: [],
  recovery: 'none',
};

function fromDocument(document: EditorDocument): ServerCopy {
  return {
    content: document.content,
    hasByteOrderMark: document.hasByteOrderMark,
    version: document.version,
    permissions: document.permissions,
  };
}

export function editorDocumentReducer(state: EditorDocumentState, action: EditorDocumentAction): EditorDocumentState {
  switch (action.type) {
    case 'loaded':
      return {
        ...state,
        status: 'ready',
        error: null,
        server: fromDocument(action.document),
        draft: action.document.content,
        conflict: null,
        saving: false,
        recovery: 'none',
      };
    case 'loadFailed':
      return { ...state, status: 'failed', error: action.error };
    case 'edited':
      return { ...state, draft: action.text };
    case 'saveStarted':
      return { ...state, saving: true, error: null, warnings: [] };
    case 'saved':
      if (state.server === null) {
        return state;
      }
      return {
        ...state,
        saving: false,
        conflict: null,
        server: { ...state.server, content: action.content, version: action.saved.version },
        warnings: action.saved.warnings,
        recovery: 'none',
      };
    case 'conflicted':
      return { ...state, saving: false, conflict: action.conflict };
    case 'saveFailed':
      return { ...state, saving: false, error: action.error };
    case 'saveAborted':
      return { ...state, saving: false };
    case 'reloadedFromConflict':
      return reloadFromConflict(state);
    case 'recovered':
      return recoverDraft(state, action.content, action.baseVersion);
  }
}

function recoverDraft(state: EditorDocumentState, content: string, baseVersion: FileVersion | null): EditorDocumentState {
  if (state.server === null) {
    return state;
  }
  const changedOnServer = baseVersion !== null && baseVersion.sha256 !== state.server.version?.sha256;
  const server = changedOnServer ? { ...state.server, version: baseVersion } : state.server;
  return { ...state, server, draft: content, recovery: changedOnServer ? 'changed' : 'unchanged' };
}

function reloadFromConflict(state: EditorDocumentState): EditorDocumentState {
  if (state.conflict === null || state.server === null) {
    return state;
  }
  const content = state.conflict.currentContent ?? '';
  const server: ServerCopy = {
    ...state.server,
    content,
    hasByteOrderMark: state.conflict.currentHasByteOrderMark,
    version: state.conflict.currentVersion,
  };
  return { ...state, server, draft: content, conflict: null, error: null, recovery: 'none' };
}

export function isDirty(state: EditorDocumentState): boolean {
  return state.server !== null && state.draft !== state.server.content;
}

export function lineEndingOf(text: string): 'CRLF' | 'LF' {
  return text.includes('\r\n') ? 'CRLF' : 'LF';
}

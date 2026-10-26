import type { EditorDocument, FileVersion, SaveConflict, SavedFile } from '../api/editor';

export interface ServerCopy {
  content: string;
  hasByteOrderMark: boolean;
  version: FileVersion | null;
  permissions: string;
}

export interface EditorDocumentState {
  status: 'loading' | 'ready' | 'failed';
  error: string | null;
  server: ServerCopy | null;
  draft: string;
  conflict: SaveConflict | null;
  saving: boolean;
  warnings: string[];
}

export type EditorDocumentAction =
  | { type: 'loaded'; document: EditorDocument }
  | { type: 'loadFailed'; error: string }
  | { type: 'edited'; text: string }
  | { type: 'saveStarted' }
  | { type: 'saved'; saved: SavedFile; content: string }
  | { type: 'conflicted'; conflict: SaveConflict }
  | { type: 'saveFailed'; error: string }
  | { type: 'reloadedFromConflict' };

export const INITIAL_DOCUMENT_STATE: EditorDocumentState = {
  status: 'loading',
  error: null,
  server: null,
  draft: '',
  conflict: null,
  saving: false,
  warnings: [],
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
      };
    case 'conflicted':
      return { ...state, saving: false, conflict: action.conflict };
    case 'saveFailed':
      return { ...state, saving: false, error: action.error };
    case 'reloadedFromConflict':
      return reloadFromConflict(state);
  }
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
  return { ...state, server, draft: content, conflict: null, error: null };
}

export function isDirty(state: EditorDocumentState): boolean {
  return state.server !== null && state.draft !== state.server.content;
}

export function lineEndingOf(text: string): 'CRLF' | 'LF' {
  return text.includes('\r\n') ? 'CRLF' : 'LF';
}

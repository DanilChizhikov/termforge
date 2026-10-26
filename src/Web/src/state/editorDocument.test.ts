import { describe, expect, it } from 'vitest';
import type { EditorDocument, SaveConflict } from '../api/editor';
import { editorDocumentReducer, INITIAL_DOCUMENT_STATE, isDirty, lineEndingOf } from './editorDocument';

const DOCUMENT: EditorDocument = {
  path: '/etc/hosts',
  content: '127.0.0.1 localhost\n',
  hasByteOrderMark: false,
  version: { modifiedUtc: '2026-10-26T10:00:00Z', size: 20, sha256: 'a'.repeat(64) },
  permissions: 'rw-r--r--',
};

const CONFLICT: SaveConflict = {
  code: 'Conflict',
  detail: 'changed',
  currentVersion: { modifiedUtc: '2026-10-26T10:01:00Z', size: 3, sha256: 'b'.repeat(64) },
  currentContent: 'new',
  currentHasByteOrderMark: true,
  operationId: 'op',
};

function loaded() {
  return editorDocumentReducer(INITIAL_DOCUMENT_STATE, { type: 'loaded', document: DOCUMENT });
}

describe('editorDocumentReducer', () => {
  it('is clean right after loading', () => {
    expect(isDirty(loaded())).toBe(false);
  });

  it('is dirty after an edit and clean after the save adopts the new version', () => {
    const edited = editorDocumentReducer(loaded(), { type: 'edited', text: 'changed' });
    const saving = editorDocumentReducer(edited, { type: 'saveStarted' });
    const savedVersion = { modifiedUtc: '2026-10-26T10:02:00Z', size: 7, sha256: 'c'.repeat(64) };
    const saved = editorDocumentReducer(saving, {
      type: 'saved',
      saved: { version: savedVersion, backupName: null, strategy: 'AtomicRename', warnings: ['w'], operationId: 'op' },
      content: 'changed',
    });

    expect(isDirty(edited)).toBe(true);
    expect(saving.saving).toBe(true);
    expect(isDirty(saved)).toBe(false);
    expect(saved.server?.version).toEqual(savedVersion);
    expect(saved.warnings).toEqual(['w']);
  });

  it('keeps the draft on conflict and replaces it only when reloading from the server', () => {
    const edited = editorDocumentReducer(loaded(), { type: 'edited', text: 'mine' });
    const conflicted = editorDocumentReducer(edited, { type: 'conflicted', conflict: CONFLICT });
    const reloaded = editorDocumentReducer(conflicted, { type: 'reloadedFromConflict' });

    expect(conflicted.draft).toBe('mine');
    expect(conflicted.conflict).toBe(CONFLICT);
    expect(reloaded.draft).toBe('new');
    expect(reloaded.server?.version).toEqual(CONFLICT.currentVersion);
    expect(reloaded.server?.hasByteOrderMark).toBe(true);
    expect(reloaded.conflict).toBeNull();
  });

  it('records save failures without touching the draft', () => {
    const edited = editorDocumentReducer(loaded(), { type: 'edited', text: 'mine' });
    const failed = editorDocumentReducer(edited, { type: 'saveFailed', error: 'boom' });

    expect(failed.error).toBe('boom');
    expect(failed.draft).toBe('mine');
    expect(isDirty(failed)).toBe(true);
  });
});

describe('lineEndingOf', () => {
  it('detects windows line endings', () => {
    expect(lineEndingOf('a\r\nb')).toBe('CRLF');
    expect(lineEndingOf('a\nb')).toBe('LF');
  });
});

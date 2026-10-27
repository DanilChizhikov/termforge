import type { FileVersion } from '../api/editor';
import type { WorkspaceEditorDraft } from '../api/workspace';
import { editorFileKey } from './editorFiles';

export interface EditorDraft {
  connectionId: string;
  path: string;
  content: string;
  baseVersion: FileVersion | null;
}

let drafts: ReadonlyMap<string, EditorDraft> = new Map();
let listeners: Array<() => void> = [];

function update(next: ReadonlyMap<string, EditorDraft>): void {
  drafts = next;
  listeners.forEach((listener) => listener());
}

export function subscribeEditorDrafts(listener: () => void): () => void {
  listeners = [...listeners, listener];
  return () => {
    listeners = listeners.filter((item) => item !== listener);
  };
}

export function getEditorDrafts(): ReadonlyMap<string, EditorDraft> {
  return drafts;
}

export function getEditorDraft(key: string): EditorDraft | null {
  return drafts.get(key) ?? null;
}

export function setEditorDraft(key: string, draft: EditorDraft): void {
  const current = drafts.get(key);
  if (current?.content === draft.content && current.baseVersion?.sha256 === draft.baseVersion?.sha256) {
    return;
  }
  const next = new Map(drafts);
  next.set(key, draft);
  update(next);
}

export function clearEditorDraft(key: string): void {
  if (!drafts.has(key)) {
    return;
  }
  const next = new Map(drafts);
  next.delete(key);
  update(next);
}

export function restoreEditorDrafts(restored: readonly WorkspaceEditorDraft[]): void {
  const next = new Map<string, EditorDraft>();
  for (const draft of restored) {
    next.set(editorFileKey(draft.connectionId, draft.path), { ...draft });
  }
  update(next);
}

export function resetEditorDrafts(): void {
  update(new Map());
}

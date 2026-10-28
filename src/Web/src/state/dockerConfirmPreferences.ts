import { useSyncExternalStore } from 'react';

export type SkippableConfirmation = 'containerRemove' | 'imageRemove';

type SkippedConfirmations = Readonly<Record<SkippableConfirmation, boolean>>;

const STORAGE_KEYS: Record<SkippableConfirmation, string> = {
  containerRemove: 'termforge.confirm.containerRemove.skip',
  imageRemove: 'termforge.confirm.imageRemove.skip',
};

const SKIPPED_VALUE = 'true';

let listeners: Array<() => void> = [];
let current: SkippedConfirmations = readStored();

function readStoredFlag(kind: SkippableConfirmation): boolean {
  try {
    return window.localStorage.getItem(STORAGE_KEYS[kind]) === SKIPPED_VALUE;
  } catch {
    return false;
  }
}

function readStored(): SkippedConfirmations {
  return {
    containerRemove: readStoredFlag('containerRemove'),
    imageRemove: readStoredFlag('imageRemove'),
  };
}

function writeStoredFlag(kind: SkippableConfirmation, skipped: boolean): boolean {
  try {
    if (skipped) {
      window.localStorage.setItem(STORAGE_KEYS[kind], SKIPPED_VALUE);
    } else {
      window.localStorage.removeItem(STORAGE_KEYS[kind]);
    }
    return true;
  } catch {
    return false;
  }
}

function subscribe(listener: () => void): () => void {
  listeners = [...listeners, listener];
  return () => {
    listeners = listeners.filter((item) => item !== listener);
  };
}

function getSkippedConfirmations(): SkippedConfirmations {
  return current;
}

export function isConfirmationSkipped(kind: SkippableConfirmation): boolean {
  return current[kind];
}

export function setConfirmationSkipped(kind: SkippableConfirmation, skipped: boolean): void {
  if (current[kind] === skipped) {
    return;
  }
  const stored = writeStoredFlag(kind, skipped);
  if (!stored && skipped) {
    return;
  }
  current = { ...current, [kind]: skipped };
  listeners.forEach((listener) => listener());
}

export function reloadConfirmationPreferences(): void {
  current = readStored();
  listeners.forEach((listener) => listener());
}

export function useSkippedConfirmations(): SkippedConfirmations {
  return useSyncExternalStore(subscribe, getSkippedConfirmations, getSkippedConfirmations);
}

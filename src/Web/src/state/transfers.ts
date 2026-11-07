import { useSyncExternalStore } from 'react';
import { describeFileError } from '../api/files';
import { apiClient } from '../api/client';
import { requestConfirmationToken } from '../api/confirmations';
import { readConflicts, requestTransferRetryConfirmation, retryTransfer, startTransfers, type TransferRecord, type TransferStartRequest } from '../api/transfers';

export interface PendingConflict {
  request: TransferStartRequest;
  conflicts: string[];
  retryId?: string;
  confirmationById?: boolean;
}

export interface TransfersState {
  transfers: readonly TransferRecord[];
  pendingConflict: PendingConflict | null;
  error: string | null;
}

const INITIAL_STATE: TransfersState = { transfers: [], pendingConflict: null, error: null };

let state: TransfersState = INITIAL_STATE;
let listeners: Array<() => void> = [];

function subscribe(listener: () => void): () => void {
  listeners = [...listeners, listener];
  return () => {
    listeners = listeners.filter((item) => item !== listener);
  };
}

function update(next: Partial<TransfersState>): void {
  state = { ...state, ...next };
  listeners.forEach((listener) => listener());
}

export function useTransfersState(): TransfersState {
  return useSyncExternalStore(
    subscribe,
    () => state,
    () => INITIAL_STATE,
  );
}

export function replaceTransfers(transfers: readonly TransferRecord[]): void {
  update({ transfers });
}

export function upsertTransfer(transfer: TransferRecord): void {
  const exists = state.transfers.some((item) => item.id === transfer.id);
  const transfers = exists
    ? state.transfers.map((item) => (item.id === transfer.id ? transfer : item))
    : [...state.transfers, transfer];
  update({ transfers });
}

export function removeTransfer(id: string): void {
  update({ transfers: state.transfers.filter((item) => item.id !== id) });
}

export function reportTransferError(error: unknown): void {
  update({ error: describeFileError(error) });
}

export function dismissTransferError(): void {
  update({ error: null });
}

export async function requestTransfers(request: TransferStartRequest): Promise<void> {
  update({ error: null });
  try {
    const result = await startTransfers(request);
    if (result.kind === 'conflict') {
      update({ pendingConflict: { request, conflicts: result.conflicts } });
      return;
    }
    result.transfers.forEach(upsertTransfer);
  } catch (error) {
    reportTransferError(error);
  }
}

export async function confirmOverwrite(): Promise<void> {
  const pending = state.pendingConflict;
  if (pending === null) {
    return;
  }
  update({ pendingConflict: null });
  if (pending.retryId !== undefined) {
    await requestTransferRetry(pending.retryId, { ...pending.request, overwrite: true }, pending.confirmationById);
    return;
  }
  await requestTransfers({ ...pending.request, overwrite: true });
}

export async function requestTransferRetry(id: string, request: TransferStartRequest, confirmationById = false): Promise<void> {
  update({ error: null });
  try {
    const options: { overwrite: boolean; confirmationToken?: string } = { overwrite: request.overwrite };
    if (request.overwrite) {
      if (confirmationById) {
        options.confirmationToken = await requestTransferRetryConfirmation(id);
      } else {
        const operation = request.direction === 'download' ? 'LocalTransferOverwrite' : 'TransferOverwrite';
        const scope = { operation, connectionId: request.targetConnectionId ?? request.connectionId, target: request.targetDirectory } as const;
        options.confirmationToken = await requestConfirmationToken(scope);
      }
    }
    await retryTransfer(id, apiClient, options);
  } catch (error) {
    const conflicts = readConflicts(error);
    if (conflicts !== null) {
      update({ pendingConflict: { request, conflicts, retryId: id, confirmationById } });
      return;
    }
    reportTransferError(error);
  }
}

export function dismissConflict(): void {
  update({ pendingConflict: null });
}

export function resetTransfers(): void {
  state = INITIAL_STATE;
  listeners.forEach((listener) => listener());
}

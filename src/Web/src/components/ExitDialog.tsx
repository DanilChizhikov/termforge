import { useCallback, useEffect, useState } from 'react';
import { ApiError } from '../api/client';
import {
  attentionCount,
  cancelExit,
  getPendingAttention,
  requestExit,
  type ExitState,
  type PendingAttention,
} from '../api/lifecycle';
import { baseName, type TransferRecord } from '../api/transfers';
import { showAppView } from '../state/appView';
import { openEditorFile } from '../state/editorFiles';
import { flushWorkspaceState } from '../state/workspacePersistence';
import { InfoIcon } from './icons/Icons';
import { LoadingState } from './LoadingState';

export const EXIT_POLL_INTERVAL_MS = 1000;

type ExitPhase =
  | { kind: 'loading' }
  | { kind: 'review'; attention: PendingAttention | null; error: string | null }
  | { kind: 'waiting'; transfers: TransferRecord[] }
  | { kind: 'stopping' }
  | { kind: 'stopped' };

export interface ExitDialogProps {
  onClose: () => void;
  pollIntervalMs?: number;
}

const COUNT_WORDS = ['Nothing', 'One thing', 'Two things', 'Three things'];

function plural(count: number, singular: string, pluralForm: string): string {
  return `${count} ${count === 1 ? singular : pluralForm}`;
}

function describeTransfer(transfer: TransferRecord): string {
  const name = baseName(transfer.sourcePath);
  return transfer.direction === 'upload' ? `${name} → ${transfer.server}` : `${name} ← ${transfer.server}`;
}

function transferPercent(transfer: TransferRecord): number | undefined {
  if (transfer.totalBytes <= 0) {
    return undefined;
  }
  return Math.min(100, Math.round((transfer.transferredBytes / transfer.totalBytes) * 100));
}

function describeAttention(attention: PendingAttention): string {
  const count = attentionCount(attention);
  if (count === 0) {
    return 'This stops the local backend. Nothing needs your attention.';
  }
  const words = COUNT_WORDS[count] ?? `${count} things`;
  return `This stops the local backend. ${words} ${count === 1 ? 'needs' : 'need'} your attention first.`;
}

function uniqueServers(attention: PendingAttention): string {
  return [...new Set(attention.terminalSessions.map((session) => session.server))].join(', ');
}

function isBackendUnreachable(error: unknown): boolean {
  return !(error instanceof ApiError);
}

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : 'Unknown error';
}

function phaseForState(state: ExitState, transfers: TransferRecord[]): ExitPhase {
  return state === 'waitingForTransfers' ? { kind: 'waiting', transfers } : { kind: 'stopping' };
}

function TransferRows({ transfers }: { transfers: readonly TransferRecord[] }) {
  return (
    <ul className="exit-transfer-list">
      {transfers.map((transfer) => (
        <li key={transfer.id} className="exit-transfer-row">
          <code className="exit-transfer-name" title={`${transfer.sourcePath} → ${transfer.targetPath}`}>
            {describeTransfer(transfer)}
          </code>
          <progress
            className="progress"
            max={100}
            value={transferPercent(transfer)}
            aria-label={`${describeTransfer(transfer)} progress`}
          />
        </li>
      ))}
    </ul>
  );
}

interface AttentionListProps {
  attention: PendingAttention;
  onReview: (connectionId: string, path: string) => void;
}

function AttentionList({ attention, onReview }: AttentionListProps) {
  const { transfers, unsavedFiles, terminalSessions } = attention;
  const firstUnsaved = unsavedFiles[0];
  return (
    <ul className="exit-attention" aria-label="Needs attention">
      {transfers.length > 0 && (
        <li className="exit-attention-item">
          <span className="status-dot status-dot-active exit-attention-dot" aria-hidden="true" />
          <div className="exit-attention-content">
            <span className="exit-attention-title">{plural(transfers.length, 'transfer', 'transfers')} in progress</span>
            <TransferRows transfers={transfers} />
            <span className="exit-attention-detail">
              Exiting now cancels them. Cancelled transfers can be retried from the journal later.
            </span>
          </div>
        </li>
      )}
      {firstUnsaved !== undefined && (
        <li className="exit-attention-item">
          <span className="status-dot status-dot-neutral exit-attention-dot" aria-hidden="true" />
          <div className="exit-attention-content">
            <span className="exit-attention-heading">
              <span className="exit-attention-title">{plural(unsavedFiles.length, 'unsaved file', 'unsaved files')}</span>
              <button
                type="button"
                className="exit-link"
                onClick={() => onReview(firstUnsaved.connectionId, firstUnsaved.path)}
              >
                Review
              </button>
            </span>
            {unsavedFiles.map((file) => (
              <span key={`${file.connectionId}:${file.path}`} className="exit-attention-detail">
                <code title={file.path}>{baseName(file.path)}</code> on {file.server} — draft is kept and restored next
                launch.
              </span>
            ))}
          </div>
        </li>
      )}
      {terminalSessions.length > 0 && (
        <li className="exit-attention-item">
          <span className="status-dot status-dot-ok exit-attention-dot" aria-hidden="true" />
          <div className="exit-attention-content">
            <span className="exit-attention-title">
              {plural(terminalSessions.length, 'terminal session', 'terminal sessions')}
            </span>
            <span className="exit-attention-detail">
              Open on {uniqueServers(attention)}. {terminalSessions.length === 1 ? 'It closes' : 'They close'}; the
              tabs come back on next launch.
            </span>
          </div>
        </li>
      )}
    </ul>
  );
}

function BrowserTabNote({ idleShutdownMinutes }: { idleShutdownMinutes: number }) {
  const host = window.location.host;
  return (
    <p className="exit-note">
      <span className="exit-note-icon">
        <InfoIcon />
      </span>
      {idleShutdownMinutes > 0 ? (
        <span>
          Just closing the browser tab? TermForge keeps running and stops after{' '}
          {plural(idleShutdownMinutes, 'minute', 'minutes')} without an open tab — reopen <code>{host}</code> to
          continue.
        </span>
      ) : (
        <span>
          Just closing the browser tab? That doesn’t stop anything — reopen <code>{host}</code> to continue.
        </span>
      )}
    </p>
  );
}

function StatusCard({ title, message }: { title: string; message: string }) {
  return (
    <div className="dialog-backdrop">
      <div role="alertdialog" aria-modal="true" aria-labelledby="exit-status-title" className="dialog-card">
        <div className="dialog-header">
          <h2 id="exit-status-title" className="dialog-title">
            {title}
          </h2>
          <LoadingState message={message} />
        </div>
      </div>
    </div>
  );
}

export function ExitDialog({ onClose, pollIntervalMs = EXIT_POLL_INTERVAL_MS }: ExitDialogProps) {
  const [phase, setPhase] = useState<ExitPhase>({ kind: 'loading' });
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let active = true;
    const load = async () => {
      await flushWorkspaceState();
      try {
        const attention = await getPendingAttention();
        if (active) {
          setPhase(
            attention.state === 'running'
              ? { kind: 'review', attention, error: null }
              : phaseForState(attention.state, attention.transfers),
          );
        }
      } catch (error) {
        if (active) {
          setPhase({ kind: 'review', attention: null, error: errorMessage(error) });
        }
      }
    };
    void load();
    return () => {
      active = false;
    };
  }, []);

  const polling = phase.kind === 'waiting' || phase.kind === 'stopping';
  useEffect(() => {
    if (!polling) {
      return undefined;
    }
    let active = true;
    const poll = async () => {
      try {
        const attention = await getPendingAttention();
        if (!active) {
          return;
        }
        if (attention.state === 'running') {
          onClose();
          return;
        }
        setPhase(phaseForState(attention.state, attention.transfers));
      } catch (error) {
        if (active && isBackendUnreachable(error)) {
          setPhase({ kind: 'stopped' });
        }
      }
    };
    const timer = setInterval(() => void poll(), pollIntervalMs);
    return () => {
      active = false;
      clearInterval(timer);
    };
  }, [polling, pollIntervalMs, onClose]);

  const exit = useCallback(async (waitForTransfers: boolean) => {
    setBusy(true);
    try {
      await flushWorkspaceState();
      const accepted = await requestExit(waitForTransfers);
      setPhase(phaseForState(accepted.state, []));
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        setPhase({ kind: 'stopping' });
      } else {
        setPhase((current) =>
          current.kind === 'review' ? { ...current, error: errorMessage(error) } : current,
        );
      }
    } finally {
      setBusy(false);
    }
  }, []);

  const keepWorking = useCallback(async () => {
    setBusy(true);
    try {
      await cancelExit();
    } catch {
      setBusy(false);
      return;
    }
    onClose();
  }, [onClose]);

  const review = useCallback(
    (connectionId: string, path: string) => {
      onClose();
      showAppView('workspace');
      openEditorFile(connectionId, path);
    },
    [onClose],
  );

  if (phase.kind === 'loading') {
    return <StatusCard title="Exit TermForge?" message="Checking for work that needs your attention…" />;
  }

  if (phase.kind === 'stopping') {
    return <StatusCard title="Exiting TermForge…" message="Finishing pending work and stopping the local backend." />;
  }

  if (phase.kind === 'stopped') {
    return (
      <StatusCard
        title="TermForge has stopped"
        message="The local backend is no longer running. You can close this tab and start TermForge again to continue."
      />
    );
  }

  if (phase.kind === 'waiting') {
    return (
      <div className="dialog-backdrop">
        <div role="alertdialog" aria-modal="true" aria-labelledby="exit-waiting-title" className="dialog-card">
          <div className="dialog-header">
            <h2 id="exit-waiting-title" className="dialog-title">
              Waiting for transfers
            </h2>
            <p className="text-secondary">
              TermForge exits as soon as {plural(phase.transfers.length, 'transfer', 'transfers')} finish. New work is
              paused until then.
            </p>
          </div>
          <div className="dialog-body">
            <TransferRows transfers={phase.transfers} />
          </div>
          <div className="dialog-footer">
            <button type="button" className="btn btn-danger-link" disabled={busy} onClick={() => void exit(false)}>
              Exit now
            </button>
            <div className="dialog-footer-spacer" />
            <button type="button" className="btn" autoFocus disabled={busy} onClick={() => void keepWorking()}>
              Keep working
            </button>
          </div>
        </div>
      </div>
    );
  }

  const { attention, error } = phase;
  const hasTransfers = attention !== null && attention.transfers.length > 0;
  return (
    <div className="dialog-backdrop">
      <div role="alertdialog" aria-modal="true" aria-labelledby="exit-dialog-title" className="dialog-card">
        <div className="dialog-header">
          <h2 id="exit-dialog-title" className="dialog-title">
            Exit TermForge?
          </h2>
          <p className="text-secondary">
            {attention === null ? 'This stops the local backend.' : describeAttention(attention)}
          </p>
        </div>
        <div className="dialog-body">
          {attention !== null && attentionCount(attention) > 0 && <AttentionList attention={attention} onReview={review} />}
          {error !== null && (
            <p role="alert" className="notice notice-danger">
              {attention === null ? 'Pending work could not be checked: ' : 'Exit failed: '}
              {error}
            </p>
          )}
          <BrowserTabNote idleShutdownMinutes={attention?.idleShutdownMinutes ?? 0} />
        </div>
        <div className="dialog-footer">
          {hasTransfers ? (
            <>
              <button type="button" className="btn btn-danger-link" disabled={busy} onClick={() => void exit(false)}>
                Exit now
              </button>
              <div className="dialog-footer-spacer" />
              <button type="button" className="btn" autoFocus onClick={onClose}>
                Cancel
              </button>
              <button type="button" className="btn btn-primary" disabled={busy} onClick={() => void exit(true)}>
                Exit after transfers finish
              </button>
            </>
          ) : (
            <>
              <button type="button" className="btn" autoFocus onClick={onClose}>
                Cancel
              </button>
              <button type="button" className="btn btn-primary" disabled={busy} onClick={() => void exit(false)}>
                Exit
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  );
}

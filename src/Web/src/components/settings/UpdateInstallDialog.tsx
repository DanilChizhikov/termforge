import { useEffect, useRef, useState } from 'react';
import { ApiError } from '../../api/client';
import { cancelInstallation, getInstallationStatus, installPreparedUpdate, prepareInstallation, type InstallationStatus } from '../../api/installation';
import { getPendingAttention, type PendingAttention } from '../../api/lifecycle';
import { flushWorkspaceStateStrict } from '../../state/workspacePersistence';

interface UpdateInstallDialogProps {
  rollback: boolean;
  onClose: () => void;
}

function describeError(error: unknown): string {
  if (error instanceof ApiError && typeof error.body === 'object' && error.body !== null && 'detail' in error.body) {
    return String(error.body.detail);
  }
  return error instanceof Error ? error.message : 'The update could not continue.';
}

export function UpdateInstallDialog({ rollback, onClose }: UpdateInstallDialogProps) {
  const [status, setStatus] = useState<InstallationStatus | null>(null);
  const [attention, setAttention] = useState<PendingAttention | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [restarting, setRestarting] = useState(false);
  const [saveCurrent, setSaveCurrent] = useState(true);
  const dialog = useRef<HTMLDialogElement>(null);
  const operation = useRef(false);
  const expectedOperation = rollback ? 'rollback' : 'update';
  const matchingOperation = status?.operation === expectedOperation;

  useEffect(() => {
    const element = dialog.current;
    element?.showModal();
    return () => element?.close();
  }, []);

  useEffect(() => {
    let active = true;
    const load = async () => {
      try {
        const current = await getInstallationStatus();
        if (!active) return;
        setStatus(current);
        if (current.state === 'idle' || current.state === 'failed') {
          await prepareInstallation(rollback);
        }
      } catch (failure) { if (active) setError(describeError(failure)); }
    };
    void load();
    return () => { active = false; };
  }, [rollback]);

  useEffect(() => {
    if (restarting) return;
    let active = true;
    let polling = false;
    const poll = async () => {
      if (polling || operation.current) return;
      polling = true;
      try {
        const current = await getInstallationStatus();
        if (!active) return;
        setStatus(current);
        if (current.error) setError(current.error);
        if (current.state === 'prepared' || current.state === 'waitingForTransfers') {
          await flushWorkspaceStateStrict();
          const work = await getPendingAttention();
          if (active) setAttention(work);
        }
      } catch (failure) { if (active) setError(describeError(failure)); }
      finally { polling = false; }
    };
    const timer = window.setInterval(() => void poll(), 1000);
    return () => { active = false; window.clearInterval(timer); };
  }, [restarting]);

  const install = async (waitForTransfers: boolean) => {
    if (operation.current) return;
    operation.current = true;
    setBusy(true);
    setError(null);
    try {
      await flushWorkspaceStateStrict();
      const accepted = await installPreparedUpdate(waitForTransfers, saveCurrent);
      if (accepted.state === 'restarting') setRestarting(true);
      else setStatus(accepted);
    } catch (failure) { setError(describeError(failure)); }
    finally { operation.current = false; setBusy(false); }
  };

  useEffect(() => {
    if (status?.readyToRestart && matchingOperation && error === null && !operation.current && !restarting) {
      void install(false);
    }
  });

  const cancel = async () => {
    if (busy || restarting) return;
    setBusy(true);
    try {
      const current = await getInstallationStatus();
      if (current.state !== 'idle' && current.state !== 'failed') await cancelInstallation();
      onClose();
    } catch (failure) { setError(describeError(failure)); }
    finally { setBusy(false); }
  };

  const hasTransfers = (attention?.transfers.length ?? 0) > 0;
  const retryWaiting = status?.state === 'waitingForTransfers' && status.readyToRestart && error !== null;
  const ready = ((status?.state === 'prepared' && attention !== null) || retryWaiting) && matchingOperation && !busy;
  const mismatched = status?.operation != null && !matchingOperation;
  return (
    <dialog ref={dialog} className="dialog-card update-install-dialog" aria-labelledby="update-install-title"
      onCancel={(event) => { event.preventDefault(); void cancel(); }}>
      <div className="dialog-header">
        <h2 id="update-install-title" className="dialog-title">{rollback ? 'Roll back TermForge?' : 'Update TermForge?'}</h2>
        <p className="text-secondary">{restarting ? 'Restarting. TermForge will open a new private launch link when it is ready.'
          : status?.state === 'preparing' ? 'Downloading and verifying the application. You can keep working until restart.'
            : status?.state === 'waitingForTransfers' ? 'Waiting for transfers. New work is paused; drafts are still saved.'
              : 'A backup is created before application files change. Terminal sessions close during restart.'}</p>
      </div>
      <div className="dialog-body">
        {status?.version && <p>Target version: <code>{status.version}</code></p>}
        {rollback && <>
          <p className="notice notice-caution">Rollback restores data from before the update. Changes made after that update will be lost.</p>
          <label className="update-retain"><input type="checkbox" checked={saveCurrent} disabled={busy || restarting}
            onChange={(event) => setSaveCurrent(event.target.checked)} />Save a copy of current data before rollback</label>
        </>}
        {attention && !restarting && <ul className="update-attention" aria-label="Work affected by restart">
          {attention.transfers.map((transfer) => <li key={transfer.id}>Transfer: <code>{transfer.sourcePath}</code></li>)}
          {attention.terminalSessions.map((terminal) => <li key={`${terminal.connectionId}:${terminal.sessionNumber}`}>Terminal {terminal.sessionNumber} on {terminal.server} — closes on restart</li>)}
          {attention.unsavedFiles.map((file) => <li key={`${file.connectionId}:${file.path}`}>Unsaved: <code>{file.path}</code> on {file.server} — draft will be restored</li>)}
          {!hasTransfers && attention.terminalSessions.length === 0 && attention.unsavedFiles.length === 0 && <li>No pending work.</li>}
        </ul>}
        {error && <p role="alert" className="notice notice-danger">{error}</p>}
        {mismatched && <p role="alert" className="notice notice-caution">Another {status.operation} operation is already prepared. Cancel it before choosing a different operation.</p>}
        {(status?.state === 'preparing' || restarting) && <p role="status">{restarting ? 'If startup fails, TermForge opens recovery before touching your data.' : 'Preparing verified files…'}</p>}
      </div>
      <div className="dialog-footer">
        <button type="button" className="btn" autoFocus disabled={busy || restarting} onClick={() => void cancel()}>Cancel</button>
        {hasTransfers && matchingOperation && (ready || status?.state === 'waitingForTransfers') && <button type="button" className="btn btn-danger-link" disabled={busy || restarting} onClick={() => void install(false)}>Cancel transfers and restart</button>}
        <button type="button" className="btn btn-primary" disabled={!ready || restarting} onClick={() => void install(!retryWaiting)}>
          {retryWaiting ? 'Retry restart' : hasTransfers ? 'Restart after transfers finish' : rollback ? 'Roll back and restart' : 'Update and restart'}
        </button>
      </div>
    </dialog>
  );
}

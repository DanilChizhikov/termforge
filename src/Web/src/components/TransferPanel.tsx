import {
  baseName,
  canResume,
  cancelTransfer,
  clearFinishedTransfers,
  describeAmount,
  describeRate,
  discardTransfer,
  formatBytes,
  isActive,
  parentDirectoryOf,
  percentOf,
  retryTransfer,
  type TransferRecord,
} from '../api/transfers';
import {
  confirmOverwrite,
  dismissConflict,
  dismissTransferError,
  reportTransferError,
  useTransfersState,
} from '../state/transfers';
import { activateWorkspaceTab } from '../state/workspaceTab';
import { ArrowDownIcon, ArrowUpIcon, CloseIcon } from './icons/Icons';
import { TransferConflictDialog } from './TransferConflictDialog';
import { useTransferFeed } from './useTransferFeed';

const STATUS_LABELS: Record<TransferRecord['status'], string> = {
  queued: 'Queued',
  running: 'Running',
  succeeded: 'Done',
  failed: 'Failed',
  cancelled: 'Cancelled',
};

const STATUS_STYLES: Record<TransferRecord['status'], { dot: string; text: string }> = {
  queued: { dot: 'status-dot status-dot-sm status-dot-idle', text: 'status-text status-text-idle' },
  running: { dot: 'status-dot status-dot-sm status-dot-active', text: 'status-text status-text-active' },
  succeeded: { dot: 'status-dot status-dot-sm status-dot-ok', text: 'status-text status-text-ok' },
  failed: { dot: 'status-dot status-dot-sm status-dot-danger', text: 'status-text status-text-danger' },
  cancelled: { dot: 'status-dot status-dot-sm status-dot-idle', text: 'status-text status-text-idle' },
};

function run(action: () => Promise<void>): void {
  action().catch(reportTransferError);
}

function summarize(transfers: readonly TransferRecord[]): string {
  const running = transfers.filter((transfer) => transfer.status === 'running').length;
  const queued = transfers.filter((transfer) => transfer.status === 'queued').length;
  const failed = transfers.filter((transfer) => transfer.status === 'failed').length;
  return `${running} active · ${queued} queued · ${failed} failed`;
}

function describeRoute(transfer: TransferRecord): string {
  const folderSuffix = transfer.totalFiles > 1 ? '/' : '';
  return `${baseName(transfer.sourcePath)}${folderSuffix} → ${parentDirectoryOf(transfer.targetPath)}`;
}

function directionClassName(transfer: TransferRecord): string {
  if (transfer.status === 'failed') {
    return 'transfer-direction transfer-direction-failed';
  }
  return transfer.direction === 'upload' ? 'transfer-direction' : 'transfer-direction transfer-direction-down';
}

function TransferCard({ transfer }: { transfer: TransferRecord }) {
  const status = STATUS_STYLES[transfer.status];
  const failed = transfer.status === 'failed';
  const resumable = canResume(transfer);
  const progressClass = failed ? 'progress progress-danger' : 'progress';
  return (
    <li className={failed ? 'transfer-card transfer-card-failed' : 'transfer-card'} aria-label={describeRoute(transfer)}>
      <div className="transfer-card-header">
        <span className={directionClassName(transfer)}>
          {transfer.direction === 'upload' ? <ArrowUpIcon size={13} /> : <ArrowDownIcon size={13} />}
          <span className="visually-hidden">{transfer.direction === 'upload' ? 'Upload' : 'Download'}</span>
        </span>
        <code className="transfer-route" title={`${transfer.sourcePath} → ${transfer.targetPath}`}>
          {describeRoute(transfer)}
        </code>
        <span className={status.text}>
          <span className={status.dot} aria-hidden="true" />
          {STATUS_LABELS[transfer.status]}
        </span>
        {isActive(transfer) && (
          <button type="button" className="btn btn-xs" onClick={() => run(() => cancelTransfer(transfer.id))}>
            Cancel
          </button>
        )}
        {resumable && (
          <button type="button" className="btn btn-xs btn-safe" onClick={() => run(() => retryTransfer(transfer.id))}>
            {transfer.partialBytes > 0 ? 'Resume' : 'Retry'}
          </button>
        )}
        {!isActive(transfer) && transfer.partialBytes > 0 && (
          <button type="button" className="btn btn-xs btn-danger-link" onClick={() => run(() => discardTransfer(transfer.id))}>
            Discard partial
          </button>
        )}
      </div>
      <progress
        className={progressClass}
        aria-label={`${baseName(transfer.sourcePath)} progress`}
        max={100}
        value={percentOf(transfer)}
      />
      <div className="transfer-card-meta">
        {failed ? (
          <span className="transfer-error">{transfer.error ?? 'Transfer failed.'}</span>
        ) : (
          <span>{describeAmount(transfer)}</span>
        )}
        <span>
          {failed && transfer.partialBytes > 0 ? `resumable from ${formatBytes(transfer.partialBytes)}` : describeRate(transfer)}
        </span>
      </div>
    </li>
  );
}

export function TransferPanel() {
  const live = useTransferFeed();
  const { transfers, pendingConflict, error } = useTransfersState();
  const hasFinished = transfers.some((transfer) => !isActive(transfer) && transfer.partialBytes === 0);

  return (
    <section aria-label="Transfers" className="transfer-panel">
      <div className="transfer-panel-header">
        <span className="transfer-panel-title">Transfers</span>
        <span className="text-secondary">{summarize(transfers)}</span>
        {!live && <span className="text-muted">Live progress disconnected.</span>}
        <span className="transfer-panel-spacer" />
        <button type="button" className="link-button" onClick={() => activateWorkspaceTab('history')}>
          Open in journal
        </button>
        <button
          type="button"
          className="btn btn-xs btn-ghost"
          disabled={!hasFinished}
          onClick={() => run(() => clearFinishedTransfers())}
        >
          Clear finished
        </button>
      </div>
      {error !== null && (
        <p role="alert" className="notice notice-danger transfer-panel-error">
          {error}
          <button type="button" className="icon-btn icon-btn-sm" aria-label="Dismiss transfer error" onClick={dismissTransferError}>
            <CloseIcon size={12} />
          </button>
        </p>
      )}
      {transfers.length === 0 ? (
        <p className="text-muted transfer-panel-empty">
          Select files and use Upload or Download, or drag them between the local and remote panes.
        </p>
      ) : (
        <ul className="transfer-list">
          {transfers.map((transfer) => (
            <TransferCard key={transfer.id} transfer={transfer} />
          ))}
        </ul>
      )}
      {pendingConflict !== null && (
        <TransferConflictDialog
          pending={pendingConflict}
          onOverwrite={() => void confirmOverwrite()}
          onCancel={dismissConflict}
        />
      )}
    </section>
  );
}

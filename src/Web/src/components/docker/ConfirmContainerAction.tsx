import type { ContainerAction } from '../../api/docker';

export interface PendingContainerAction {
  container: string;
  action: ContainerAction;
}

export interface ConfirmContainerActionProps {
  pending: PendingContainerAction;
  serverName: string;
  onConfirm: () => void;
  onCancel: () => void;
}

const CONSEQUENCES: Partial<Record<ContainerAction, string>> = {
  stop: 'Its processes are stopped and anything it serves is unavailable until it is started again.',
  restart: 'Its processes are stopped and started again; open connections to it are dropped.',
};

export function ConfirmContainerAction({ pending, serverName, onConfirm, onCancel }: ConfirmContainerActionProps) {
  const verb = pending.action === 'stop' ? 'Stop' : 'Restart';
  return (
    <div className="dialog-backdrop">
      <div role="dialog" aria-modal="true" aria-labelledby="container-action-title" className="dialog-card">
        <div className="dialog-header">
          <h2 id="container-action-title" className="dialog-title">
            {verb} <code>{pending.container}</code> on {serverName}?
          </h2>
          <p className="text-secondary">{CONSEQUENCES[pending.action]} The action is recorded in the journal.</p>
        </div>
        <div className="dialog-footer">
          <button type="button" className="btn btn-destructive" onClick={onConfirm}>
            {verb} container
          </button>
          <button type="button" className="btn btn-safe" autoFocus onClick={onCancel}>
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
}

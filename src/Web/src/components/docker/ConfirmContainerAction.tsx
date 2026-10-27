import type { KeyboardEvent } from 'react';
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

  const closeOnEscape = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape') {
      event.stopPropagation();
      onCancel();
    }
  };

  return (
    <div
      role="dialog"
      aria-modal="false"
      aria-labelledby="container-action-title"
      className="docker-confirm"
      onKeyDown={closeOnEscape}
    >
      <p id="container-action-title" className="docker-confirm-title">
        {verb} {pending.container} on {serverName}?
      </p>
      <p className="docker-confirm-text">{CONSEQUENCES[pending.action]} The action is recorded in the journal.</p>
      <div className="docker-confirm-actions">
        <button type="button" className="btn btn-sm btn-safe" autoFocus onClick={onCancel}>
          Cancel
        </button>
        <button type="button" className="btn btn-sm btn-destructive" onClick={onConfirm}>
          {verb} container
        </button>
      </div>
    </div>
  );
}

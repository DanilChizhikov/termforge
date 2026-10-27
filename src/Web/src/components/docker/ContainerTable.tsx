import { Fragment, type ReactNode } from 'react';
import type { ContainerAction, DockerContainer } from '../../api/docker';
import {
  ACTION_LABELS,
  availableActions,
  canOpenShell,
  containerStateLabel,
  containerTone,
  containerUptime,
  offersShell,
} from './dockerStatus';
import type { PendingContainerAction } from './ConfirmContainerAction';

export interface ContainerTableProps {
  containers: DockerContainer[];
  busyContainer: string | null;
  selectedContainer: string | null;
  logsContainer: string | null;
  pending: PendingContainerAction | null;
  confirmation: ReactNode;
  onLogs: (container: DockerContainer) => void;
  onShell: (container: DockerContainer) => void;
  onAction: (container: DockerContainer, action: ContainerAction) => void;
}

const COLUMN_COUNT = 6;

function stateTextClass(container: DockerContainer): string {
  const tone = containerTone(container);
  return tone === 'ok' ? 'status-text docker-state' : `status-text status-text-${tone}`;
}

export function ContainerTable({
  containers,
  busyContainer,
  selectedContainer,
  logsContainer,
  pending,
  confirmation,
  onLogs,
  onShell,
  onAction,
}: ContainerTableProps) {
  return (
    <div className="table-card">
      <table className="data-table docker-table">
        <thead>
          <tr>
            <th>Name</th>
            <th>Image</th>
            <th>State</th>
            <th>Ports</th>
            <th>Uptime</th>
            <th className="cell-end">Actions</th>
          </tr>
        </thead>
        <tbody>
          {containers.map((container) => {
            const tone = containerTone(container);
            const isBusy = busyContainer === container.name;
            const confirming = pending !== null && pending.container === container.name;
            return (
              <Fragment key={container.id}>
                <tr className={container.name === selectedContainer ? 'docker-row-selected' : undefined}>
                  <td className="docker-name">{container.name}</td>
                  <td className="docker-image">
                    <code>{container.image}</code>
                  </td>
                  <td>
                    <span className={stateTextClass(container)}>
                      <span className={`status-dot status-dot-sm status-dot-${tone}`} aria-hidden="true" />
                      {containerStateLabel(container)}
                    </span>
                  </td>
                  <td className="docker-ports">
                    <code>{container.ports === '' ? '—' : container.ports}</code>
                  </td>
                  <td className="cell-muted" title={container.status}>
                    {containerUptime(container)}
                  </td>
                  <td className="cell-end">
                    <div className="docker-actions">
                      <button
                        type="button"
                        className="btn btn-sm docker-row-button"
                        aria-label={`Logs of ${container.name}`}
                        aria-pressed={logsContainer === container.name}
                        onClick={() => onLogs(container)}
                      >
                        Logs
                      </button>
                      {offersShell(container) ? (
                        <button
                          type="button"
                          className="btn btn-sm docker-row-button"
                          aria-label={`Shell in ${container.name}`}
                          disabled={!canOpenShell(container)}
                          onClick={() => onShell(container)}
                        >
                          Shell
                        </button>
                      ) : null}
                      {availableActions(container).map((action) => {
                        const pressed = confirming && pending.action === action;
                        return (
                          <button
                            key={action}
                            type="button"
                            className={pressed ? 'btn btn-sm docker-row-button docker-row-button-armed' : 'btn btn-sm docker-row-button'}
                            disabled={isBusy}
                            aria-label={`${ACTION_LABELS[action]} ${container.name}`}
                            aria-expanded={action === 'start' ? undefined : pressed}
                            onClick={() => onAction(container, action)}
                          >
                            {ACTION_LABELS[action]}
                          </button>
                        );
                      })}
                    </div>
                  </td>
                </tr>
                {confirming ? (
                  <tr className="docker-confirm-row">
                    <td colSpan={COLUMN_COUNT}>{confirmation}</td>
                  </tr>
                ) : null}
              </Fragment>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

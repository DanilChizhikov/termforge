import type { ContainerAction, DockerContainer } from '../../api/docker';
import { ACTION_LABELS, availableActions, canOpenShell, containerStateLabel, containerTone } from './dockerStatus';

export interface ContainerTableProps {
  containers: DockerContainer[];
  busyContainer: string | null;
  selectedContainer: string | null;
  onLogs: (container: DockerContainer) => void;
  onShell: (container: DockerContainer) => void;
  onAction: (container: DockerContainer, action: ContainerAction) => void;
}

export function ContainerTable({
  containers,
  busyContainer,
  selectedContainer,
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
            <th>Status</th>
            <th className="cell-end">Actions</th>
          </tr>
        </thead>
        <tbody>
          {containers.map((container) => {
            const tone = containerTone(container);
            const isBusy = busyContainer === container.name;
            return (
              <tr
                key={container.id}
                className={container.name === selectedContainer ? 'docker-row-selected' : undefined}
              >
                <td className="docker-name">
                  <code>{container.name}</code>
                </td>
                <td className="cell-mono docker-image">{container.image}</td>
                <td>
                  <span className={`status-text status-text-${tone}`}>
                    <span className={`status-dot status-dot-sm status-dot-${tone}`} aria-hidden="true" />
                    {containerStateLabel(container)}
                  </span>
                </td>
                <td className="cell-mono cell-muted">{container.ports === '' ? '—' : container.ports}</td>
                <td className="cell-muted">{container.status}</td>
                <td className="cell-end">
                  <div className="docker-actions">
                    <button
                      type="button"
                      className="btn btn-sm"
                      aria-label={`Logs of ${container.name}`}
                      onClick={() => onLogs(container)}
                    >
                      Logs
                    </button>
                    {canOpenShell(container) ? (
                      <button
                        type="button"
                        className="btn btn-sm"
                        aria-label={`Shell in ${container.name}`}
                        onClick={() => onShell(container)}
                      >
                        Shell
                      </button>
                    ) : null}
                    {availableActions(container).map((action) => (
                      <button
                        key={action}
                        type="button"
                        className="btn btn-sm"
                        disabled={isBusy}
                        aria-label={`${ACTION_LABELS[action]} ${container.name}`}
                        onClick={() => onAction(container, action)}
                      >
                        {ACTION_LABELS[action]}
                      </button>
                    ))}
                  </div>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

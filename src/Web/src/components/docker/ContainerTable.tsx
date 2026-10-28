import { Fragment, type ReactNode } from 'react';
import type { ComposeAction, ComposeInfo, ContainerAction, DockerContainer } from '../../api/docker';
import {
  ACTION_LABELS,
  availableActions,
  canOpenShell,
  COMPOSE_ACTION_LABELS,
  containerStateLabel,
  containerTone,
  containerUptime,
  groupByComposeProject,
  offersShell,
} from './dockerStatus';
import { SelectionCheckbox } from './SelectionCheckbox';
import type { RowSelection } from './useRowSelection';

export type PendingDockerAction =
  | { target: 'container'; name: string; action: ContainerAction }
  | { target: 'compose'; name: string; action: ComposeAction };

export interface ContainerTableProps {
  containers: DockerContainer[];
  busyContainers: ReadonlySet<string>;
  busyProject: string | null;
  selectedContainer: string | null;
  logsContainer: string | null;
  pending: PendingDockerAction | null;
  confirmation: ReactNode;
  onLogs: (container: DockerContainer) => void;
  onShell: (container: DockerContainer) => void;
  onAction: (container: DockerContainer, action: ContainerAction) => void;
  onComposeAction: (project: string, action: ComposeAction) => void;
  selection: RowSelection;
  readOnly: boolean;
}

const COLUMN_COUNT = 7;
const COMPOSE_ACTIONS: readonly ComposeAction[] = ['restart', 'recreate'];

function stateTextClass(container: DockerContainer): string {
  const tone = containerTone(container);
  return tone === 'ok' ? 'status-text docker-state' : `status-text status-text-${tone}`;
}

export function ContainerTable(props: ContainerTableProps) {
  const groups = groupByComposeProject(props.containers);
  const showGroups = groups.some((group) => group.compose !== null);
  return (
    <div className="table-card">
      <table className="data-table docker-table">
        <thead>
          <tr>
            <th className="docker-select-cell">
              <SelectionCheckbox
                label="Select all containers"
                checked={props.selection.allSelected}
                indeterminate={props.selection.someSelected}
                disabled={props.readOnly}
                onChange={props.selection.toggleAll}
              />
            </th>
            <th>Name</th>
            <th>Image</th>
            <th>State</th>
            <th>Ports</th>
            <th>Uptime</th>
            <th className="cell-end">Actions</th>
          </tr>
        </thead>
        <tbody>
          {groups.map((group) => (
            <Fragment key={group.compose?.project ?? ''}>
              {showGroups ? <GroupHeader compose={group.compose} {...props} /> : null}
              {group.containers.map((container) => (
                <ContainerRow key={container.id} container={container} {...props} />
              ))}
            </Fragment>
          ))}
        </tbody>
      </table>
    </div>
  );
}

interface GroupHeaderProps extends ContainerTableProps {
  compose: ComposeInfo | null;
}

function GroupHeader({ compose, pending, confirmation, busyProject, readOnly, onComposeAction }: GroupHeaderProps) {
  if (compose === null) {
    return (
      <tr className="docker-group-row">
        <th scope="colgroup" colSpan={COLUMN_COUNT}>
          <span className="section-label">Standalone containers</span>
        </th>
      </tr>
    );
  }
  const project = compose.project;
  const confirming = pending !== null && pending.target === 'compose' && pending.name === project;
  return (
    <>
      <tr className="docker-group-row">
        <th scope="colgroup" colSpan={COLUMN_COUNT}>
          <div className="docker-group">
            <span className="section-label">Compose</span>
            <code className="docker-group-name">{project}</code>
            <code className="docker-group-path" title={compose.workingDirectory}>
              {compose.workingDirectory}
            </code>
            <span className="docker-header-spacer" />
            {COMPOSE_ACTIONS.map((action) => {
              const pressed = confirming && pending.action === action;
              return (
                <button
                  key={action}
                  type="button"
                  className={pressed ? 'btn btn-sm docker-row-button docker-row-button-armed' : 'btn btn-sm docker-row-button'}
                  disabled={busyProject === project || readOnly}
                  aria-label={`${COMPOSE_ACTION_LABELS[action]} project ${project}`}
                  aria-expanded={pressed}
                  onClick={() => onComposeAction(project, action)}
                >
                  {COMPOSE_ACTION_LABELS[action]} project
                </button>
              );
            })}
          </div>
        </th>
      </tr>
      {confirming ? (
        <tr className="docker-confirm-row">
          <td colSpan={COLUMN_COUNT}>{confirmation}</td>
        </tr>
      ) : null}
    </>
  );
}

interface ContainerRowProps extends ContainerTableProps {
  container: DockerContainer;
}

function ContainerRow({
  container,
  busyContainers,
  selectedContainer,
  logsContainer,
  pending,
  confirmation,
  onLogs,
  onShell,
  onAction,
  selection,
  readOnly,
}: ContainerRowProps) {
  const tone = containerTone(container);
  const isBusy = busyContainers.has(container.name);
  const confirming = pending !== null && pending.target === 'container' && pending.name === container.name;
  return (
    <>
      <tr className={container.name === selectedContainer ? 'docker-row-selected' : undefined}>
        <td className="docker-select-cell">
          <SelectionCheckbox
            label={`Select ${container.name}`}
            checked={selection.selected.has(container.name)}
            disabled={readOnly}
            onChange={() => selection.toggle(container.name)}
          />
        </td>
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
                disabled={!canOpenShell(container) || readOnly}
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
                  disabled={isBusy || readOnly}
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
    </>
  );
}

import { useCallback, useState } from 'react';
import { describeError } from '../../api/connections';
import {
  ACTIONS_REQUIRING_CONFIRMATION,
  runComposeAction,
  runContainerAction,
  type ComposeAction,
  type ContainerAction,
  type DockerContainer,
  type DockerOverview,
} from '../../api/docker';
import { isConfirmationSkipped, setConfirmationSkipped } from '../../state/dockerConfirmPreferences';
import { EmptyState } from '../EmptyState';
import { BulkActionBar, type BulkActionButton } from './BulkActionBar';
import { ConfirmDockerAction } from './ConfirmDockerAction';
import { ContainerLogs } from './ContainerLogs';
import { ContainerShellPanel } from './ContainerShellPanel';
import { ContainerTable, type PendingDockerAction } from './ContainerTable';
import {
  ACTION_LABELS,
  ACTION_PAST_TENSE,
  availableActions,
  canOpenShell,
  COMPOSE_ACTION_LABELS,
  COMPOSE_ACTION_PAST_TENSE,
  isRunning,
  matchesFilter,
  type ContainerFilter,
} from './dockerStatus';
import { DockerNotice, type ActionNotice } from './DockerNotice';
import { countOf, describeBulkOutcome, runSequentially } from './runBulk';
import { useRowSelection } from './useRowSelection';

export interface ContainersPanelProps {
  serverId: string;
  overview: DockerOverview;
  filter: ContainerFilter;
  readOnly: boolean;
  onChanged: () => void;
}

type DetailPanel = { kind: 'logs'; container: string } | { kind: 'shell'; container: string } | null;

interface PendingBulkAction {
  action: ContainerAction;
  names: string[];
}

const BULK_ACTIONS: readonly ContainerAction[] = ['start', 'restart', 'stop', 'remove'];
const NO_CONTAINERS: ReadonlySet<string> = new Set();

const CONTAINER_CONSEQUENCES: Partial<Record<ContainerAction, string>> = {
  stop: 'Its processes are stopped and anything it serves is unavailable until it is started again.',
  restart: 'Its processes are stopped and started again; open connections to it are dropped.',
  remove: 'The container and its writable layer are deleted permanently; named volumes and the image stay.',
};

const RUNNING_REMOVE_CONSEQUENCE =
  'It is running and will be force-stopped first. The container and its writable layer are deleted permanently; named volumes and the image stay.';

const BULK_CONSEQUENCES: Partial<Record<ContainerAction, string>> = {
  stop: 'Their processes are stopped and anything they serve is unavailable until they are started again.',
  restart: 'Their processes are stopped and started again; open connections to them are dropped.',
  remove: 'The containers and their writable layers are deleted permanently; named volumes and images stay.',
};

const BULK_RUNNING_REMOVE_CONSEQUENCE =
  'Running ones are force-stopped first. The containers and their writable layers are deleted permanently; named volumes and images stay.';

const COMPOSE_CONSEQUENCES: Record<ComposeAction, string> = {
  restart: 'Every container of the project is restarted; open connections to them are dropped.',
  recreate:
    'The project is taken down with docker compose down and started again with up -d from its compose files. Containers are recreated; data outside volumes is lost.',
};

export function ContainersPanel({ serverId, overview, filter, readOnly, onChanged }: ContainersPanelProps) {
  const [panel, setPanel] = useState<DetailPanel>(null);
  const [pending, setPending] = useState<PendingDockerAction | null>(null);
  const [pendingBulk, setPendingBulk] = useState<PendingBulkAction | null>(null);
  const [busyContainers, setBusyContainers] = useState<ReadonlySet<string>>(NO_CONTAINERS);
  const [busyProject, setBusyProject] = useState<string | null>(null);
  const [notice, setNotice] = useState<ActionNotice | null>(null);

  const executeContainer = useCallback(
    (container: string, action: ContainerAction) => {
      const busy = new Set([container]);
      setBusyContainers(busy);
      setNotice(null);
      const confirmed = ACTIONS_REQUIRING_CONFIRMATION.has(action);
      runContainerAction(serverId, container, action, confirmed)
        .then(() => {
          setNotice({ tone: 'ok', message: `${ACTION_PAST_TENSE[action]} ${container}. Recorded in the journal.` });
          if (action === 'remove') {
            setPanel((current) => (current?.container === container ? null : current));
          }
          onChanged();
        })
        .catch((failure: unknown) => setNotice({ tone: 'danger', message: describeError(failure) }))
        .finally(() => setBusyContainers(NO_CONTAINERS));
    },
    [serverId, onChanged],
  );

  const containers = overview.containers;
  const visible = containers.filter((container) => matchesFilter(container, filter));
  const visibleNames = visible.map((container) => container.name);
  const selection = useRowSelection(visibleNames);
  const clearSelection = selection.clear;
  const selectedContainers = visible.filter((container) => selection.selected.has(container.name));

  const executeBulk = useCallback(
    ({ action, names }: PendingBulkAction) => {
      const busy = new Set(names);
      setBusyContainers(busy);
      setNotice(null);
      const confirmed = ACTIONS_REQUIRING_CONFIRMATION.has(action);
      runSequentially(names, (name) => runContainerAction(serverId, name, action, confirmed))
        .then((outcome) => {
          const wording = { pastTense: ACTION_PAST_TENSE[action], one: 'container', many: 'containers' };
          setNotice(describeBulkOutcome(outcome, wording));
          if (action === 'remove') {
            setPanel((current) => (current !== null && outcome.succeeded.includes(current.container) ? null : current));
          }
          clearSelection();
          onChanged();
        })
        .finally(() => setBusyContainers(NO_CONTAINERS));
    },
    [serverId, onChanged, clearSelection],
  );

  const requestBulk = (action: ContainerAction) => {
    const names = selectedContainers
      .filter((container) => availableActions(container).includes(action))
      .map((container) => container.name);
    const bulk = { action, names };
    const skipped = action === 'remove' && isConfirmationSkipped('containerRemove');
    if (ACTIONS_REQUIRING_CONFIRMATION.has(action) && !skipped) {
      setPending(null);
      setPendingBulk(bulk);
      return;
    }
    executeBulk(bulk);
  };

  const confirmBulk = (skipNextTime: boolean) => {
    if (pendingBulk !== null) {
      if (pendingBulk.action === 'remove' && skipNextTime) {
        setConfirmationSkipped('containerRemove', true);
      }
      executeBulk(pendingBulk);
    }
    setPendingBulk(null);
  };

  const executeCompose = useCallback(
    (project: string, action: ComposeAction) => {
      setBusyProject(project);
      setNotice(null);
      runComposeAction(serverId, project, action)
        .then(() => {
          setNotice({ tone: 'ok', message: `${COMPOSE_ACTION_PAST_TENSE[action]} project ${project}. Recorded in the journal.` });
          onChanged();
        })
        .catch((failure: unknown) => setNotice({ tone: 'danger', message: describeError(failure) }))
        .finally(() => setBusyProject(null));
    },
    [serverId, onChanged],
  );

  const requestAction = useCallback(
    (container: DockerContainer, action: ContainerAction) => {
      const skipped = action === 'remove' && isConfirmationSkipped('containerRemove');
      if (ACTIONS_REQUIRING_CONFIRMATION.has(action) && !skipped) {
        setPendingBulk(null);
        setPending({ target: 'container', name: container.name, action });
        return;
      }
      executeContainer(container.name, action);
    },
    [executeContainer],
  );

  const confirmPending = useCallback(
    (skipNextTime: boolean) => {
      if (pending?.target === 'container') {
        if (pending.action === 'remove' && skipNextTime) {
          setConfirmationSkipped('containerRemove', true);
        }
        executeContainer(pending.name, pending.action);
      }
      if (pending?.target === 'compose') {
        executeCompose(pending.name, pending.action);
      }
      setPending(null);
    },
    [pending, executeContainer, executeCompose],
  );

  const bulkButtons: BulkActionButton<ContainerAction>[] = BULK_ACTIONS.flatMap((action) => {
    const count = selectedContainers.filter((container) => availableActions(container).includes(action)).length;
    if (count === 0) {
      return [];
    }
    const ariaLabel = `${ACTION_LABELS[action]} ${countOf(count, 'selected container', 'selected containers')}`;
    return [{ action, label: `${ACTION_LABELS[action]} ${count}`, ariaLabel, confirms: ACTIONS_REQUIRING_CONFIRMATION.has(action) }];
  });
  const panelContainer = containers.find((container) => container.name === panel?.container) ?? null;

  return (
    <>
      <DockerNotice notice={notice} />
      {selection.someSelected ? (
        <BulkActionBar
          summary={`${selection.selected.size} selected`}
          buttons={bulkButtons}
          armedAction={pendingBulk?.action ?? null}
          disabled={readOnly || busyContainers.size > 0}
          confirmation={
            pendingBulk !== null ? (
              <BulkConfirmation
                pending={pendingBulk}
                serverName={overview.serverName}
                containers={containers}
                onConfirm={confirmBulk}
                onCancel={() => setPendingBulk(null)}
              />
            ) : null
          }
          onAction={requestBulk}
          onClear={() => {
            setPendingBulk(null);
            clearSelection();
          }}
        />
      ) : null}
      {visible.length > 0 ? (
        <ContainerTable
          containers={visible}
          busyContainers={busyContainers}
          busyProject={busyProject}
          selectedContainer={panel?.container ?? null}
          logsContainer={panel?.kind === 'logs' ? panel.container : null}
          pending={pending}
          readOnly={readOnly}
          selection={selection}
          confirmation={
            pending !== null ? (
              <PendingConfirmation
                pending={pending}
                serverName={overview.serverName}
                containers={containers}
                onConfirm={confirmPending}
                onCancel={() => setPending(null)}
              />
            ) : null
          }
          onLogs={(container) => setPanel({ kind: 'logs', container: container.name })}
          onShell={(container) => setPanel({ kind: 'shell', container: container.name })}
          onAction={requestAction}
          onComposeAction={(project, action) => {
            setPendingBulk(null);
            setPending({ target: 'compose', name: project, action });
          }}
        />
      ) : (
        <EmptyState message={containers.length === 0 ? 'No containers on this server.' : 'No containers match the filter.'} />
      )}
      {panel?.kind === 'logs' ? (
        <ContainerLogs
          key={panel.container}
          connectionId={serverId}
          container={panel.container}
          canOpenShell={panelContainer !== null && canOpenShell(panelContainer)}
          onOpenShell={() => setPanel({ kind: 'shell', container: panel.container })}
          onClose={() => setPanel(null)}
        />
      ) : null}
      {panel?.kind === 'shell' ? (
        <ContainerShellPanel key={panel.container} connectionId={serverId} container={panel.container} onClose={() => setPanel(null)} />
      ) : null}
    </>
  );
}

interface PendingConfirmationProps {
  pending: PendingDockerAction;
  serverName: string;
  containers: DockerContainer[];
  onConfirm: (skipNextTime: boolean) => void;
  onCancel: () => void;
}

function PendingConfirmation({ pending, serverName, containers, onConfirm, onCancel }: PendingConfirmationProps) {
  if (pending.target === 'compose') {
    const label = COMPOSE_ACTION_LABELS[pending.action];
    return (
      <ConfirmDockerAction
        title={`${label} project ${pending.name} on ${serverName}?`}
        consequence={COMPOSE_CONSEQUENCES[pending.action]}
        confirmLabel={`${label} project`}
        onConfirm={onConfirm}
        onCancel={onCancel}
      />
    );
  }
  const label = ACTION_LABELS[pending.action];
  const target = containers.find((container) => container.name === pending.name);
  const removesRunning = pending.action === 'remove' && target !== undefined && isRunning(target);
  const consequence = removesRunning ? RUNNING_REMOVE_CONSEQUENCE : (CONTAINER_CONSEQUENCES[pending.action] ?? '');
  return (
    <ConfirmDockerAction
      title={`${label} ${pending.name} on ${serverName}?`}
      consequence={consequence}
      confirmLabel={`${label} container`}
      offerSkip={pending.action === 'remove'}
      onConfirm={onConfirm}
      onCancel={onCancel}
    />
  );
}

interface BulkConfirmationProps {
  pending: PendingBulkAction;
  serverName: string;
  containers: DockerContainer[];
  onConfirm: (skipNextTime: boolean) => void;
  onCancel: () => void;
}

function BulkConfirmation({ pending, serverName, containers, onConfirm, onCancel }: BulkConfirmationProps) {
  const label = ACTION_LABELS[pending.action];
  const targets = containers.filter((container) => pending.names.includes(container.name));
  const removesRunning = pending.action === 'remove' && targets.some(isRunning);
  const consequence = removesRunning ? BULK_RUNNING_REMOVE_CONSEQUENCE : (BULK_CONSEQUENCES[pending.action] ?? '');
  const subject = countOf(pending.names.length, 'container', 'containers');
  return (
    <ConfirmDockerAction
      title={`${label} ${subject} on ${serverName}?`}
      consequence={`${pending.names.join(', ')}. ${consequence}`}
      confirmLabel={`${label} ${subject}`}
      offerSkip={pending.action === 'remove'}
      onConfirm={onConfirm}
      onCancel={onCancel}
    />
  );
}

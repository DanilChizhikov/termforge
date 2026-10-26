import { useCallback, useEffect, useState } from 'react';
import { describeError } from '../../api/connections';
import {
  ACTIONS_REQUIRING_CONFIRMATION,
  getDockerOverview,
  runContainerAction,
  type ContainerAction,
  type DockerContainer,
  type DockerOverview,
} from '../../api/docker';
import { useSelectedServerId } from '../../state/selectedServer';
import { EmptyState } from '../EmptyState';
import { RefreshIcon } from '../icons/Icons';
import { ConfirmContainerAction, type PendingContainerAction } from './ConfirmContainerAction';
import { ContainerLogs } from './ContainerLogs';
import { ContainerShellPanel } from './ContainerShellPanel';
import { ContainerTable } from './ContainerTable';
import {
  ACTION_PAST_TENSE,
  AVAILABILITY_TITLES,
  availabilityTone,
  canOpenShell,
  isProblem,
  isRunning,
  matchesFilter,
  type ContainerFilter,
} from './dockerStatus';

interface OverviewState {
  overview: DockerOverview | null;
  error: string | null;
  isLoading: boolean;
}

type DetailPanel = { kind: 'logs'; container: string } | { kind: 'shell'; container: string } | null;

interface ActionNotice {
  tone: 'ok' | 'danger';
  message: string;
}

export function DockerWorkspace({ active }: { active: boolean }) {
  const serverId = useSelectedServerId();
  const [wasActivated, setWasActivated] = useState(active);
  if (active && !wasActivated) {
    setWasActivated(true);
  }
  if (!wasActivated) {
    return null;
  }
  if (serverId === null) {
    return <EmptyState message="Select a server to manage Docker containers." />;
  }
  return <DockerView key={serverId} serverId={serverId} />;
}

function DockerView({ serverId }: { serverId: string }) {
  const [state, setState] = useState<OverviewState>({ overview: null, error: null, isLoading: true });
  const [filter, setFilter] = useState<ContainerFilter>('all');
  const [panel, setPanel] = useState<DetailPanel>(null);
  const [pending, setPending] = useState<PendingContainerAction | null>(null);
  const [busyContainer, setBusyContainer] = useState<string | null>(null);
  const [notice, setNotice] = useState<ActionNotice | null>(null);

  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let active = true;
    getDockerOverview(serverId)
      .then((overview) => {
        if (active) {
          setState({ overview, error: null, isLoading: false });
        }
      })
      .catch((failure: unknown) => {
        if (active) {
          setState((previous) => ({ ...previous, error: describeError(failure), isLoading: false }));
        }
      });
    return () => {
      active = false;
    };
  }, [serverId, reloadKey]);

  const refresh = useCallback(() => {
    setState((previous) => ({ ...previous, error: null, isLoading: true }));
    setReloadKey((key) => key + 1);
  }, []);

  const execute = useCallback(
    (container: string, action: ContainerAction) => {
      setBusyContainer(container);
      setNotice(null);
      const confirmed = ACTIONS_REQUIRING_CONFIRMATION.has(action);
      runContainerAction(serverId, container, action, confirmed)
        .then(() => {
          setNotice({ tone: 'ok', message: `${ACTION_PAST_TENSE[action]} ${container}. Recorded in the journal.` });
          refresh();
        })
        .catch((failure: unknown) => setNotice({ tone: 'danger', message: describeError(failure) }))
        .finally(() => setBusyContainer(null));
    },
    [serverId, refresh],
  );

  const requestAction = useCallback(
    (container: DockerContainer, action: ContainerAction) => {
      if (ACTIONS_REQUIRING_CONFIRMATION.has(action)) {
        setPending({ container: container.name, action });
        return;
      }
      execute(container.name, action);
    },
    [execute],
  );

  const confirmPending = useCallback(() => {
    if (pending !== null) {
      execute(pending.container, pending.action);
    }
    setPending(null);
  }, [pending, execute]);

  const overview = state.overview;
  const containers = overview?.containers ?? [];
  const visible = containers.filter((container) => matchesFilter(container, filter));
  const panelContainer = containers.find((container) => container.name === panel?.container) ?? null;

  return (
    <section aria-label="Docker containers" className="view docker-view">
      <div className="view-header">
        <div className="view-header-text">
          <h2 className="page-title">
            Containers
            {overview !== null ? (
              <>
                {' '}
                <span className="docker-title-server">on {overview.serverName}</span>
              </>
            ) : null}
          </h2>
        </div>
        {overview !== null ? <AvailabilityBadge overview={overview} /> : null}
        {overview?.availability.state === 'Available' ? (
          <div role="group" aria-label="Filter containers" className="segmented">
            <FilterButton filter="all" current={filter} label={`All ${containers.length}`} onSelect={setFilter} />
            <FilterButton
              filter="running"
              current={filter}
              label={`Running ${containers.filter(isRunning).length}`}
              onSelect={setFilter}
            />
            <FilterButton
              filter="problems"
              current={filter}
              label={`Problems ${containers.filter(isProblem).length}`}
              onSelect={setFilter}
            />
          </div>
        ) : null}
        <button type="button" className="icon-btn" aria-label="Refresh containers" disabled={state.isLoading} onClick={refresh}>
          <RefreshIcon />
        </button>
      </div>
      {state.isLoading && overview === null ? <p className="text-secondary">Checking Docker on the server...</p> : null}
      {state.error !== null ? (
        <p role="alert" className="notice notice-danger">
          {state.error}
        </p>
      ) : null}
      {notice !== null ? (
        <p role={notice.tone === 'danger' ? 'alert' : 'status'} className={notice.tone === 'danger' ? 'notice notice-danger' : 'notice'}>
          {notice.message}
        </p>
      ) : null}
      {overview !== null && overview.availability.state !== 'Available' ? (
        <UnavailableCallout overview={overview} />
      ) : null}
      {overview?.availability.state === 'Available' ? (
        visible.length > 0 ? (
          <ContainerTable
            containers={visible}
            busyContainer={busyContainer}
            selectedContainer={panel?.container ?? null}
            onLogs={(container) => setPanel({ kind: 'logs', container: container.name })}
            onShell={(container) => setPanel({ kind: 'shell', container: container.name })}
            onAction={requestAction}
          />
        ) : (
          <EmptyState message={containers.length === 0 ? 'No containers on this server.' : 'No containers match the filter.'} />
        )
      ) : null}
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
        <ContainerShellPanel
          key={panel.container}
          connectionId={serverId}
          container={panel.container}
          onClose={() => setPanel(null)}
        />
      ) : null}
      {pending !== null && overview !== null ? (
        <ConfirmContainerAction
          pending={pending}
          serverName={overview.serverName}
          onConfirm={confirmPending}
          onCancel={() => setPending(null)}
        />
      ) : null}
    </section>
  );
}

function AvailabilityBadge({ overview }: { overview: DockerOverview }) {
  const tone = availabilityTone(overview.availability.state);
  return (
    <span className={`status-text status-text-${tone}`}>
      <span className={`status-dot status-dot-sm status-dot-${tone}`} aria-hidden="true" />
      {AVAILABILITY_TITLES[overview.availability.state]}
    </span>
  );
}

function UnavailableCallout({ overview }: { overview: DockerOverview }) {
  return (
    <div role="alert" className="callout">
      <span className="callout-title callout-danger">{AVAILABILITY_TITLES[overview.availability.state]}</span>
      <span className="text-secondary">{overview.availability.reason}</span>
      <span className="text-muted">Fix it on the server, then refresh. The Diagnostics tab shows every capability check.</span>
    </div>
  );
}

interface FilterButtonProps {
  filter: ContainerFilter;
  current: ContainerFilter;
  label: string;
  onSelect: (filter: ContainerFilter) => void;
}

function FilterButton({ filter, current, label, onSelect }: FilterButtonProps) {
  return (
    <button type="button" aria-pressed={filter === current} onClick={() => onSelect(filter)}>
      {label}
    </button>
  );
}

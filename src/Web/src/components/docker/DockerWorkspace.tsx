import { useCallback, useEffect, useState } from 'react';
import { describeError } from '../../api/connections';
import { getDockerOverview, type DockerOverview } from '../../api/docker';
import { useConnection } from '../../state/connections';
import { useSelectedServerId } from '../../state/selectedServer';
import { EmptyState } from '../EmptyState';
import { CheckIcon, RefreshArcIcon } from '../icons/Icons';
import { ContainersPanel } from './ContainersPanel';
import { AVAILABILITY_TITLES, availabilityTone, isProblem, isRunning, type ContainerFilter } from './dockerStatus';
import { ImagesPanel } from './ImagesPanel';

interface OverviewState {
  overview: DockerOverview | null;
  error: string | null;
  isLoading: boolean;
}

type DockerSection = 'containers' | 'images';

const SECTION_TITLES: Record<DockerSection, string> = {
  containers: 'Containers',
  images: 'Images',
};

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
  const connection = useConnection(serverId);
  const readOnly = connection?.readOnly ?? false;
  const [state, setState] = useState<OverviewState>({ overview: null, error: null, isLoading: true });
  const [section, setSection] = useState<DockerSection>('containers');
  const [filter, setFilter] = useState<ContainerFilter>('all');
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

  const overview = state.overview;
  const containers = overview?.containers ?? [];
  const isAvailable = overview?.availability.state === 'Available';

  return (
    <section aria-label="Docker containers" className="view docker-view">
      <div className="view-header docker-header">
        <h2 className="page-title">
          {SECTION_TITLES[section]}
          {overview !== null ? (
            <>
              {' '}
              <span className="docker-title-server">on {overview.serverName}</span>
            </>
          ) : null}
        </h2>
        {overview !== null ? <AvailabilityBadge overview={overview} /> : null}
        <span className="docker-header-spacer" />
        {isAvailable ? (
          <div role="group" aria-label="Docker section" className="segmented">
            <SectionButton section="containers" current={section} onSelect={setSection} />
            <SectionButton section="images" current={section} onSelect={setSection} />
          </div>
        ) : null}
        {isAvailable && section === 'containers' ? (
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
        <button
          type="button"
          className="icon-btn icon-btn-lg icon-btn-outlined"
          aria-label="Refresh"
          title={`Refresh ${SECTION_TITLES[section].toLowerCase()}`}
          disabled={state.isLoading}
          onClick={refresh}
        >
          <RefreshArcIcon />
        </button>
      </div>
      {state.isLoading && overview === null ? <p className="text-secondary">Checking Docker on the server...</p> : null}
      {state.error !== null ? (
        <p role="alert" className="notice notice-danger">
          {state.error}
        </p>
      ) : null}
      {readOnly ? (
        <p role="status" className="notice notice-caution">
          {connection?.name ?? 'This server'} is read-only. Starting, stopping, restarting, removing, pulling, compose actions
          and opening shells are blocked; logs stay available.
        </p>
      ) : null}
      {overview !== null && !isAvailable ? <UnavailableCallout overview={overview} /> : null}
      {overview !== null && isAvailable && section === 'containers' ? (
        <ContainersPanel serverId={serverId} overview={overview} filter={filter} readOnly={readOnly} onChanged={refresh} />
      ) : null}
      {overview !== null && isAvailable && section === 'images' ? (
        <ImagesPanel
          serverId={serverId}
          serverName={overview.serverName}
          containers={containers}
          readOnly={readOnly}
          reloadKey={reloadKey}
          onPulled={refresh}
        />
      ) : null}
    </section>
  );
}

function AvailabilityBadge({ overview }: { overview: DockerOverview }) {
  const tone = availabilityTone(overview.availability.state);
  if (tone === 'ok') {
    return (
      <span className="docker-availability" title={overview.availability.reason}>
        <CheckIcon size={12} />
        {AVAILABILITY_TITLES[overview.availability.state]}
      </span>
    );
  }
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

interface SectionButtonProps {
  section: DockerSection;
  current: DockerSection;
  onSelect: (section: DockerSection) => void;
}

function SectionButton({ section, current, onSelect }: SectionButtonProps) {
  return (
    <button type="button" aria-pressed={section === current} onClick={() => onSelect(section)}>
      {SECTION_TITLES[section]}
    </button>
  );
}

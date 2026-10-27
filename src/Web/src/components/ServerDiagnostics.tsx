import { useCallback, useEffect, useState } from 'react';
import {
  getCapabilities,
  refreshCapabilities,
  type CapabilityReport,
  type CapabilityStatus,
} from '../api/diagnostics';
import { useConnection } from '../state/connections';
import {
  MONITORING_INTERVALS_SECONDS,
  setMonitoringInterval,
  useMonitoringInterval,
} from '../state/monitoringInterval';
import { useSelectedServerId } from '../state/selectedServer';
import { resumeMetrics, useServerMetrics } from '../state/serverMetrics';
import { EmptyState } from './EmptyState';
import { CheckIcon, RefreshArcIcon } from './icons/Icons';
import { ServerMetricsSection } from './ServerMetricsSection';
import { statusDotClass, type StatusTone } from './statusTone';
import { formatClock } from './timeFormat';

interface DiagnosticsState {
  report: CapabilityReport | null;
  error: string | null;
  isLoading: boolean;
}

const LOAD_ERROR = 'Could not load diagnostics.';

export function ServerDiagnostics() {
  const serverId = useSelectedServerId();
  if (serverId === null) {
    return <EmptyState message="Select a server to see its diagnostics." />;
  }
  return <DiagnosticsView key={serverId} serverId={serverId} />;
}

function DiagnosticsView({ serverId }: { serverId: string }) {
  const [state, setState] = useState<DiagnosticsState>({ report: null, error: null, isLoading: true });
  const connection = useConnection(serverId);
  const metrics = useServerMetrics(serverId);
  const intervalSeconds = useMonitoringInterval();

  useEffect(() => {
    let active = true;
    getCapabilities(serverId)
      .then((report) => {
        if (active) {
          setState({ report, error: null, isLoading: false });
        }
      })
      .catch(() => {
        if (active) {
          setState((previous) => ({ ...previous, error: LOAD_ERROR, isLoading: false }));
        }
      });
    return () => {
      active = false;
    };
  }, [serverId]);

  const refresh = useCallback(() => {
    setState((previous) => ({ ...previous, error: null, isLoading: true }));
    resumeMetrics(serverId);
    refreshCapabilities(serverId)
      .then((report) => setState({ report, error: null, isLoading: false }))
      .catch(() => setState((previous) => ({ ...previous, error: LOAD_ERROR, isLoading: false })));
  }, [serverId]);

  return (
    <section aria-label="Server diagnostics" className="view diagnostics-view">
      <div className="view-header">
        <div className="view-header-text">
          <h2 className="page-title">{connection?.name ?? 'Server diagnostics'}</h2>
          {connection === null ? null : (
            <code className="view-subtitle">
              {connection.userName}@{connection.host}:{connection.port}
            </code>
          )}
        </div>
        <span className="view-subtitle">
          {describeChecked(state.report, metrics.metrics?.collectedAt ?? null)}
          {metrics.isPolling ? ` · every ${intervalSeconds} s while open` : ' · polling stopped'}
        </span>
        <label className="diagnostics-interval">
          <span className="visually-hidden">Interval</span>
          <select
            className="input input-sm"
            aria-label="Interval"
            value={intervalSeconds}
            onChange={(event) => setMonitoringInterval(Number(event.target.value))}
          >
            {MONITORING_INTERVALS_SECONDS.map((seconds) => (
              <option key={seconds} value={seconds}>
                every {seconds} s
              </option>
            ))}
          </select>
        </label>
        <button type="button" className="btn btn-sm btn-filled" disabled={state.isLoading} onClick={refresh}>
          <RefreshArcIcon size={14} />
          Run checks again
        </button>
      </div>
      <ServerMetricsSection serverId={serverId} />
      {state.isLoading ? <p className="text-secondary">Checking server…</p> : null}
      {state.error !== null ? (
        <p role="alert" className="notice notice-danger">
          {state.error}
        </p>
      ) : null}
      {state.report !== null ? <ReportView report={state.report} /> : null}
    </section>
  );
}

function describeChecked(report: CapabilityReport | null, collectedAt: string | null): string {
  const checkedAt = collectedAt ?? report?.checkedAt ?? null;
  return checkedAt === null ? 'Not checked yet' : `Checked ${formatClock(checkedAt)}`;
}

const STATUS_TONES: Record<CapabilityStatus, StatusTone> = {
  Available: 'ok',
  Unavailable: 'danger',
  Unknown: 'idle',
};

function summarize(report: CapabilityReport): string {
  const available = report.capabilities.filter((capability) => capability.status === 'Available').length;
  const unavailable = report.capabilities.filter((capability) => capability.status === 'Unavailable').length;
  const unknown = report.capabilities.length - available - unavailable;
  const parts = [`${available} available`];
  if (unavailable > 0) {
    parts.push(`${unavailable} unavailable`);
  }
  if (unknown > 0) {
    parts.push(`${unknown} unknown`);
  }
  return parts.join(' · ');
}

function CapabilityIcon({ status }: { status: CapabilityStatus }) {
  if (status === 'Available') {
    return (
      <span className="capability-icon status-text-ok">
        <CheckIcon size={16} />
      </span>
    );
  }
  return (
    <span className="capability-icon">
      <span className={statusDotClass(STATUS_TONES[status])} aria-hidden="true" />
    </span>
  );
}

function ReportView({ report }: { report: CapabilityReport }) {
  return (
    <>
      {report.state !== 'Completed' ? (
        <p role="alert" className="notice notice-danger">
          {report.message ?? report.state}
        </p>
      ) : null}
      {report.capabilities.length > 0 ? (
        <section aria-label="Capabilities" className="capability-card">
          <div className="capability-card-header">
            <span className="capability-card-title">Capabilities</span>
            <span className="view-subtitle">{summarize(report)}</span>
          </div>
          <ul className="capability-list">
            {report.capabilities.map((capability) => (
              <li key={capability.id} className="capability-row">
                <CapabilityIcon status={capability.status} />
                <span className="capability-name">{capability.name}</span>
                <span className="capability-detail">
                  {capability.status === 'Available' ? (
                    <span className="visually-hidden">Available. </span>
                  ) : (
                    <span className={`capability-status status-text-${STATUS_TONES[capability.status]}`}>{capability.status}</span>
                  )}
                  {capability.reason}
                </span>
              </li>
            ))}
          </ul>
        </section>
      ) : null}
    </>
  );
}

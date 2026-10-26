import { useCallback, useEffect, useState } from 'react';
import {
  getCapabilities,
  refreshCapabilities,
  type CapabilityReport,
  type CapabilityStatus,
} from '../api/diagnostics';
import { useSelectedServerId } from '../state/selectedServer';
import { EmptyState } from './EmptyState';
import { ServerMetricsSection } from './ServerMetricsSection';
import { RefreshIcon } from './icons/Icons';

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
    refreshCapabilities(serverId)
      .then((report) => setState({ report, error: null, isLoading: false }))
      .catch(() => setState((previous) => ({ ...previous, error: LOAD_ERROR, isLoading: false })));
  }, [serverId]);

  return (
    <section aria-label="Server diagnostics" className="view">
      <div className="view-header">
        <div className="view-header-text">
          <h2 className="page-title">Server diagnostics</h2>
          {state.report !== null ? (
            <span className="view-subtitle">Checked at {new Date(state.report.checkedAt).toLocaleString()}</span>
          ) : null}
        </div>
        <button type="button" className="btn btn-sm" disabled={state.isLoading} onClick={refresh}>
          <RefreshIcon size={13} />
          Refresh
        </button>
      </div>
      <ServerMetricsSection serverId={serverId} />
      {state.isLoading ? <p className="text-secondary">Checking server...</p> : null}
      {state.error !== null ? (
        <p role="alert" className="notice notice-danger">
          {state.error}
        </p>
      ) : null}
      {state.report !== null ? <ReportView report={state.report} /> : null}
    </section>
  );
}

const STATUS_TONES: Record<CapabilityStatus, string> = {
  Available: 'ok',
  Unavailable: 'danger',
  Unknown: 'idle',
};

function ReportView({ report }: { report: CapabilityReport }) {
  return (
    <>
      {report.state !== 'Completed' ? (
        <p role="alert" className="notice notice-danger">
          {report.message ?? report.state}
        </p>
      ) : null}
      {report.capabilities.length > 0 ? (
        <div className="table-card">
          <table className="data-table">
            <thead>
              <tr>
                <th>Capability</th>
                <th>Status</th>
                <th>Details</th>
              </tr>
            </thead>
            <tbody>
              {report.capabilities.map((capability) => (
                <tr key={capability.id}>
                  <td className="capability-name">{capability.name}</td>
                  <td>
                    <span className={`status-text status-text-${STATUS_TONES[capability.status]}`}>
                      <span
                        className={`status-dot status-dot-sm status-dot-${STATUS_TONES[capability.status]}`}
                        aria-hidden="true"
                      />
                      {capability.status}
                    </span>
                  </td>
                  <td className="cell-muted">{capability.reason}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </>
  );
}

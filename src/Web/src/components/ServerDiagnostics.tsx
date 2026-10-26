import { useCallback, useEffect, useState } from 'react';
import {
  getCapabilities,
  refreshCapabilities,
  type CapabilityReport,
} from '../api/diagnostics';
import { useSelectedServerId } from '../state/selectedServer';
import { EmptyState } from './EmptyState';

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
    <section aria-label="Server diagnostics">
      <h2>Server diagnostics</h2>
      <button type="button" disabled={state.isLoading} onClick={refresh}>
        Refresh
      </button>
      {state.isLoading ? <p>Checking server...</p> : null}
      {state.error !== null ? <p role="alert">{state.error}</p> : null}
      {state.report !== null ? <ReportView report={state.report} /> : null}
    </section>
  );
}

function ReportView({ report }: { report: CapabilityReport }) {
  return (
    <div>
      <p>Checked at {new Date(report.checkedAt).toLocaleString()}</p>
      {report.state !== 'Completed' ? <p role="alert">{report.message ?? report.state}</p> : null}
      {report.capabilities.length > 0 ? (
        <table className="operation-table">
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
                <td>{capability.name}</td>
                <td>{capability.status}</td>
                <td>{capability.reason}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}
    </div>
  );
}

import { useState } from 'react';
import type { DiagnosticResult, ProbeOutcome } from '../api/networkDiagnostics';
import { changeDiagnosticPage, useNetworkDiagnostics } from '../state/networkDiagnostics';
import { NetworkDiagnosticDialog } from './NetworkDiagnosticDialog';
import { statusDotClass, type StatusTone } from './statusTone';

const OUTCOMES: Record<ProbeOutcome['status'], { label: string; tone: StatusTone }> = {
  responded: { label: 'Responded', tone: 'ok' }, refused: { label: 'Refused', tone: 'danger' },
  noResponse: { label: 'No response', tone: 'caution' }, error: { label: 'Error', tone: 'danger' },
  unavailable: { label: 'Unavailable', tone: 'idle' },
};

function Outcome({ outcome }: { outcome: ProbeOutcome }) {
  const display = OUTCOMES[outcome.status];
  return <span className="network-outcome"><span className={statusDotClass(display.tone)} aria-hidden="true" />{display.label}</span>;
}

export function NetworkDiagnosticResults() {
  const state = useNetworkDiagnostics();
  const [detail, setDetail] = useState<DiagnosticResult | null>(null);
  const { run } = state;
  if (run === null) return <section aria-label="Network results" className="network-results network-results-empty"><p className="text-secondary">Run network checks to see results.</p><p className="field-hint">Checks start only when requested.</p></section>;

  return (
    <section aria-label="Network check results" className="network-results">
      <div className="network-results-header">
        <div className="network-section-header"><h3>Results <code>{run.host}</code></h3><span role="status">{run.status} · {run.completed} / {run.total}</span></div>
        <progress className="progress" aria-label="Network checks progress" value={run.completed} max={Math.max(1, run.total)} />
        <p className="field-hint">UDP silence and an unreachable host do not prove network blocking.</p>
        {run.error === null ? null : <p role="alert" className="notice notice-danger">{run.error}</p>}
      </div>
      <div className="network-table-scroll" role="region" aria-label="Scrollable network results" tabIndex={0}>
        <table className="data-table network-table">
          <colgroup><col className="network-column-point" /><col className="network-column-category" /><col className="network-column-check" /><col className="network-column-port" /><col className="network-column-result" /><col /><col className="network-column-time" /></colgroup>
          <thead><tr><th>Point</th><th>Category</th><th>Check</th><th>Port</th><th>Result</th><th>Details</th><th>Time</th></tr></thead>
          <tbody>{state.results.map((result) => <tr key={result.sequence}>
            <td>{result.pointName}</td><td>{result.point.category === 'rf' ? 'Russia' : 'Outside Russia'}</td><td>{result.protocol.toUpperCase()}</td><td><code>{result.port ?? '—'}</code></td>
            <td><Outcome outcome={result.outcome} /></td>
            <td><span className="network-detail-preview">{result.outcome.detail}</span><button type="button" className="link-button network-detail-button" aria-label={`View details for ${result.pointName} ${result.protocol.toUpperCase()}${result.port === null ? '' : ` port ${result.port}`}`} onClick={() => setDetail(result)}>View details</button></td>
            <td className="network-elapsed">{Math.round(result.elapsedMilliseconds)} ms</td>
          </tr>)}</tbody>
        </table>
      </div>
      <div className="network-pagination"><span>{state.offset + (state.results.length === 0 ? 0 : 1)}–{state.offset + state.results.length} of {run.completed}</span><div className="network-choices"><button type="button" className="btn btn-sm" disabled={state.offset === 0} onClick={() => void changeDiagnosticPage(state.offset - 100)}>Previous results</button><button type="button" className="btn btn-sm" disabled={state.offset + 100 >= run.completed} onClick={() => void changeDiagnosticPage(state.offset + 100)}>Next results</button></div></div>
      {detail === null ? null : <NetworkDiagnosticDialog title="Check details" onClose={() => setDetail(null)}>
        <div className="dialog-body network-details-body"><p className="field-hint">{detail.pointName} · {detail.point.category === 'rf' ? 'Russia' : 'Outside Russia'} · {detail.protocol.toUpperCase()} · {detail.port ?? '—'} · {Math.round(detail.elapsedMilliseconds)} ms</p><Outcome outcome={detail.outcome} /><pre className="network-command network-detail-output" tabIndex={0}>{detail.outcome.detail}</pre></div>
        <div className="dialog-footer"><button type="button" className="btn" onClick={() => setDetail(null)}>Close</button></div>
      </NetworkDiagnosticDialog>}
    </section>
  );
}

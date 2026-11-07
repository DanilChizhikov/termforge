import { NETWORK_PROTOCOLS, type DiagnosticPoint, type ProbeOutcome } from '../api/networkDiagnostics';
import { useConnectionsSnapshot } from '../state/connections';
import {
  changeDiagnosticPage, dismissDiagnosticConfirmation, runNetworkDiagnostic, stopNetworkDiagnostic,
  updateDiagnosticDraft, useNetworkDiagnostics,
} from '../state/networkDiagnostics';
import { NetworkRemoteActions } from './NetworkRemoteActions';
import { useNetworkDiagnosticFeed } from './useNetworkDiagnosticFeed';

const OUTCOMES: Record<ProbeOutcome['status'], { label: string; tone: string }> = {
  responded: { label: 'Responded', tone: 'ok' }, refused: { label: 'Refused', tone: 'danger' },
  noResponse: { label: 'No response', tone: 'caution' }, error: { label: 'Error', tone: 'danger' },
  unavailable: { label: 'Unavailable', tone: 'idle' },
};

export function NetworkDiagnosticPanel() {
  useNetworkDiagnosticFeed();
  const state = useNetworkDiagnostics();
  const connections = useConnectionsSnapshot();
  const { draft, run } = state;
  const active = run?.status === 'queued' || run?.status === 'running';
  const disabled = state.busy || active;
  const points = [{ id: null, name: 'Local machine' }, ...connections.map((connection) => ({ id: connection.id, name: connection.name }))];
  function selectPoint(id: string | null, selected: boolean) {
    const next: DiagnosticPoint[] = selected ? [...draft.points, { connectionId: id, category: 'rf' }] : draft.points.filter((point) => point.connectionId !== id);
    updateDiagnosticDraft({ points: next });
  }
  return (
    <section aria-label="Network diagnostics" className="view network-diagnostics">
      <div className="view-header"><h2 className="page-title">Network diagnostics</h2><span className="view-subtitle">Manual checks from this machine and saved Linux SSH servers</span></div>
      <form className="network-form" onSubmit={(event) => { event.preventDefault(); void runNetworkDiagnostic(); }}>
        <fieldset disabled={disabled} className="network-fields">
          <legend className="section-label">Target and checks</legend>
          <div className="network-grid">
            <label className="field"><span className="field-label">Target</span><select className="input" value={draft.targetConnectionId} onChange={(event) => updateDiagnosticDraft({ targetConnectionId: event.target.value })}>
              <option value="">Custom address</option>{connections.map((connection) => <option key={connection.id} value={connection.id}>{connection.name}</option>)}
            </select></label>
            {draft.targetConnectionId === '' ? <label className="field"><span className="field-label">Host or IP</span><input className="input input-mono" value={draft.host} placeholder="example.com" onChange={(event) => updateDiagnosticDraft({ host: event.target.value })} /></label> : null}
            <label className="field"><span className="field-label">DNS domain</span><input className="input input-mono" value={draft.domain} placeholder="Defaults to target host" onChange={(event) => updateDiagnosticDraft({ domain: event.target.value })} /></label>
            <label className="field"><span className="field-label">HTTP URL</span><input className="input input-mono" value={draft.url} placeholder="https://example.com/" onChange={(event) => updateDiagnosticDraft({ url: event.target.value })} /></label>
            <label className="field"><span className="field-label">Ports and ranges</span><input className="input input-mono" disabled={draft.allPorts} value={draft.ports} placeholder="22,80,443,8000-8100" onChange={(event) => updateDiagnosticDraft({ ports: event.target.value })} /></label>
          </div>
          <label className="network-choice"><input type="checkbox" checked={draft.allPorts} onChange={(event) => updateDiagnosticDraft({ allPorts: event.target.checked })} />All 65,535 ports — requires confirmation</label>
          <div className="network-choices" role="group" aria-label="Protocols">{NETWORK_PROTOCOLS.map((protocol) => <label key={protocol} className="network-choice"><input type="checkbox" checked={draft.protocols.includes(protocol)} onChange={(event) => updateDiagnosticDraft({ protocols: event.target.checked ? [...draft.protocols, protocol] : draft.protocols.filter((item) => item !== protocol) })} />{protocol.toUpperCase()}</label>)}</div>
        </fieldset>
        <fieldset disabled={disabled} className="network-fields">
          <legend className="section-label">Measurement points · select up to eight</legend>
          <div className="network-grid">{points.map((point) => {
            const selected = draft.points.find((item) => item.connectionId === point.id);
            return <div key={point.id ?? 'local'} className="network-point">
              <label className="network-choice"><input type="checkbox" checked={selected !== undefined} disabled={selected === undefined && draft.points.length >= 8} onChange={(event) => selectPoint(point.id, event.target.checked)} />{point.name}</label>
              {selected === undefined ? null : <label className="field"><span className="visually-hidden">Category for {point.name}</span><select className="input input-sm" value={selected.category} onChange={(event) => updateDiagnosticDraft({ points: draft.points.map((item) => item.connectionId === point.id ? { ...item, category: event.target.value as DiagnosticPoint['category'] } : item) })}><option value="rf">РФ</option><option value="outsideRf">Вне РФ</option></select></label>}
            </div>;
          })}</div>
        </fieldset>
        <p className="notice">Checks are rate limited. Missing tools affect only their checks. UDP silence and an unreachable host do not prove network blocking.</p>
        <div className="network-choices"><button className="btn btn-primary" type="submit" disabled={disabled || draft.points.length === 0 || draft.protocols.length === 0}>Run network checks</button>
          {active ? <button className="btn" type="button" disabled={state.busy} onClick={() => void stopNetworkDiagnostic()}>Cancel checks</button> : null}</div>
      </form>
      {state.pending === null ? null : <div role="alertdialog" aria-label="Confirm full port scan" className="notice notice-caution">
        <p>Scan every port on {state.pending.host ?? connections.find((connection) => connection.id === state.pending?.targetConnectionId)?.name ?? state.pending.url} from {state.pending.points.length} point(s)? This can take a long time and generate substantial network traffic. Rate limits remain enabled.</p>
        <div className="network-choices"><button className="btn btn-safe" autoFocus onClick={dismissDiagnosticConfirmation}>Keep settings</button><button className="btn" disabled={state.busy} onClick={() => void runNetworkDiagnostic(true)}>Confirm full scan</button></div>
      </div>}
      {state.error === null ? null : <p className="notice notice-danger" role="alert">{state.error}</p>}
      {run === null ? null : <section aria-label="Network check results" className="network-results">
        <div className="network-choices"><strong>{run.host}</strong><span role="status">{run.status} · {run.completed} / {run.total}</span></div>
        <progress className="progress" aria-label="Network checks progress" value={run.completed} max={Math.max(1, run.total)} />
        {run.error === null ? null : <p role="alert" className="notice notice-danger">{run.error}</p>}
        <div className="network-table-scroll"><table className="data-table"><thead><tr><th>Point</th><th>Category</th><th>Check</th><th>Port</th><th>Result</th><th>Details</th><th>Time</th></tr></thead><tbody>
          {state.results.map((result) => <tr key={result.sequence}><td>{result.pointName}</td><td>{result.point.category === 'rf' ? 'РФ' : 'Вне РФ'}</td><td>{result.protocol.toUpperCase()}</td><td>{result.port ?? '—'}</td><td><span className={`status-dot status-dot-sm status-dot-${OUTCOMES[result.outcome.status].tone}`} aria-hidden="true" /> {OUTCOMES[result.outcome.status].label}</td><td>{result.outcome.detail}</td><td>{Math.round(result.elapsedMilliseconds)} ms</td></tr>)}
        </tbody></table></div>
        <div className="network-choices"><button className="btn btn-sm" disabled={state.offset === 0} onClick={() => void changeDiagnosticPage(state.offset - 100)}>Previous results</button><span>{state.offset + (state.results.length === 0 ? 0 : 1)}–{state.offset + state.results.length} of {run.completed}</span><button className="btn btn-sm" disabled={state.offset + 100 >= run.completed} onClick={() => void changeDiagnosticPage(state.offset + 100)}>Next results</button></div>
      </section>}
      <NetworkRemoteActions />
    </section>
  );
}

import { NETWORK_PROTOCOLS } from '../api/networkDiagnostics';
import { useConnectionsSnapshot } from '../state/connections';
import {
  dismissDiagnosticConfirmation, runNetworkDiagnostic, stopNetworkDiagnostic,
  updateDiagnosticDraft, useNetworkDiagnostics,
} from '../state/networkDiagnostics';
import { NetworkRemoteActions } from './NetworkRemoteActions';
import { useNetworkDiagnosticFeed } from './useNetworkDiagnosticFeed';
import { NetworkMeasurementPoints } from './NetworkMeasurementPoints';
import { NetworkDiagnosticResults } from './NetworkDiagnosticResults';

export function NetworkDiagnosticPanel() {
  useNetworkDiagnosticFeed();
  const state = useNetworkDiagnostics();
  const connections = useConnectionsSnapshot();
  const { draft, run } = state;
  const active = run?.status === 'queued' || run?.status === 'running';
  const disabled = state.busy || active;
  return (
    <section aria-label="Network diagnostics" className="view network-diagnostics">
      <div className="view-header">
        <div className="view-header-text"><h2 className="page-title">Network diagnostics</h2><span className="view-subtitle">Check connectivity from your machine and saved Linux SSH servers</span></div>
        <div className="network-choices"><button className="btn btn-primary" type="submit" form="network-diagnostic-form" disabled={disabled || draft.points.length === 0 || draft.protocols.length === 0}>Run network checks</button>
          {active ? <button className="btn" type="button" disabled={state.busy} onClick={() => void stopNetworkDiagnostic()}>Cancel checks</button> : null}</div>
      </div>
      {state.error === null ? null : <p className="notice notice-danger" role="alert">{state.error}</p>}
      <div className="network-workspace">
        <div className="network-settings" aria-label="Check settings">
          <form id="network-diagnostic-form" className="network-form" onSubmit={(event) => { event.preventDefault(); void runNetworkDiagnostic(); }}>
            <fieldset disabled={disabled} className="network-fields">
              <legend className="section-label">Target and checks</legend>
              <div className="network-grid">
                <label className="field"><span className="field-label">Target</span><select className="input" value={draft.targetConnectionId} onChange={(event) => updateDiagnosticDraft({ targetConnectionId: event.target.value })}>
                  <option value="">Custom address</option>{connections.map((connection) => <option key={connection.id} value={connection.id}>{connection.name}</option>)}
                </select></label>
                {draft.targetConnectionId === '' ? <label className="field"><span className="field-label">Host or IP</span><input className="input input-mono" value={draft.host} placeholder="example.com" onChange={(event) => updateDiagnosticDraft({ host: event.target.value })} /></label> : null}
                <label className="field"><span className="field-label">Ports and ranges</span><input className="input input-mono" disabled={draft.allPorts} value={draft.ports} placeholder="22,80,443,8000-8100" onChange={(event) => updateDiagnosticDraft({ ports: event.target.value })} /></label>
              </div>
              <label className="network-choice"><input className="checkbox" type="checkbox" checked={draft.allPorts} onChange={(event) => updateDiagnosticDraft({ allPorts: event.target.checked })} />All 65,535 ports — requires confirmation</label>
              <div className="network-protocols" role="group" aria-label="Protocols">{NETWORK_PROTOCOLS.map((protocol) => <label key={protocol} className="network-choice"><input className="checkbox" type="checkbox" checked={draft.protocols.includes(protocol)} onChange={(event) => updateDiagnosticDraft({ protocols: event.target.checked ? [...draft.protocols, protocol] : draft.protocols.filter((item) => item !== protocol) })} />{protocol.toUpperCase()}</label>)}</div>
              <details className="network-disclosure"><summary>Additional parameters</summary><div className="network-grid">
                <label className="field"><span className="field-label">DNS domain</span><input className="input input-mono" value={draft.domain} placeholder="Defaults to target host" onChange={(event) => updateDiagnosticDraft({ domain: event.target.value })} /></label>
                <label className="field"><span className="field-label">HTTP URL</span><input className="input input-mono" value={draft.url} placeholder="https://example.com/" onChange={(event) => updateDiagnosticDraft({ url: event.target.value })} /></label>
              </div></details>
            </fieldset>
            <NetworkMeasurementPoints disabled={disabled} />
            <p className="field-hint">Checks are rate limited. Missing tools affect only their checks.</p>
          </form>
          {state.pending === null ? null : <div role="alertdialog" aria-label="Confirm full port scan" className="notice notice-caution">
            <p>Scan every port on {state.pending.host ?? connections.find((connection) => connection.id === state.pending?.targetConnectionId)?.name ?? state.pending.url} from {state.pending.points.length} point(s)? This can take a long time and generate substantial network traffic. Rate limits remain enabled.</p>
            <div className="network-choices"><button className="btn btn-safe" autoFocus onClick={dismissDiagnosticConfirmation}>Keep settings</button><button className="btn" disabled={state.busy} onClick={() => void runNetworkDiagnostic(true)}>Confirm full scan</button></div>
          </div>}
          <NetworkRemoteActions />
        </div>
        <NetworkDiagnosticResults />
      </div>
    </section>
  );
}

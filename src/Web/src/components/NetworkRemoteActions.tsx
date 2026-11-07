import { useState } from 'react';
import { describeError } from '../api/connections';
import { executeRemoteAction, prepareRemoteAction, type RemoteActionPlan, type RemoteActionRequest, type RemoteActionResult } from '../api/networkDiagnostics';
import { useConnectionsSnapshot } from '../state/connections';
import { useNetworkDiagnostics } from '../state/networkDiagnostics';

export function NetworkRemoteActions() {
  const { draft } = useNetworkDiagnostics();
  const connections = useConnectionsSnapshot();
  const [connectionId, setConnectionId] = useState('');
  const [kind, setKind] = useState<RemoteActionRequest['kind']>('install');
  const [port, setPort] = useState('53');
  const [usePassword, setUsePassword] = useState(false);
  const [password, setPassword] = useState('');
  const [plan, setPlan] = useState<RemoteActionPlan | null>(null);
  const [result, setResult] = useState<RemoteActionResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const savedTarget = connections.find((connection) => connection.id === draft.targetConnectionId);
  const points = connections.filter((connection) => draft.points.some((point) => point.connectionId === connection.id));
  async function prepare() {
    setBusy(true); setError(null); setResult(null); setPassword('');
    try {
      const host = savedTarget?.host ?? (draft.host || (draft.url === '' ? '' : new URL(draft.url).hostname));
      const next = await prepareRemoteAction({ connectionId, kind, protocols: draft.protocols,
        ...(host === '' ? {} : { host }), port: Number(port), usePassword });
      setPlan(next);
    } catch (failure) { setError(describeError(failure)); }
    finally { setBusy(false); }
  }
  async function execute() {
    if (plan === null) return;
    setBusy(true); setError(null);
    const secret = password;
    setPassword('');
    try { setResult(await executeRemoteAction(plan, secret)); }
    catch (failure) { setError(describeError(failure)); }
    finally { setPlan(null); setBusy(false); }
  }
  return <section aria-label="Remote diagnostic actions" className="network-results">
    <h3 className="section-label">Optional remote actions</h3>
    <p className="field-hint">Nothing is installed and no privileged check runs automatically. Prepare an action to review its exact command, packages and server before confirming.</p>
    <fieldset disabled={busy || plan !== null} className="network-fields"><div className="network-grid">
      <label className="field"><span className="field-label">SSH point</span><select className="input" value={connectionId} onChange={(event) => setConnectionId(event.target.value)}><option value="">Select a point…</option>{points.map((connection) => <option key={connection.id} disabled={connection.readOnly} value={connection.id}>{connection.name}{connection.readOnly ? ' · read-only' : ''}</option>)}</select></label>
      <label className="field"><span className="field-label">Action</span><select className="input" value={kind} onChange={(event) => setKind(event.target.value as RemoteActionRequest['kind'])}><option value="install">Install tools for selected protocols</option><option value="udpDetails">Privileged UDP check</option></select></label>
      {kind !== 'udpDetails' ? null : <label className="field"><span className="field-label">UDP port</span><input className="input input-mono" type="number" min="1" max="65535" value={port} onChange={(event) => setPort(event.target.value)} /></label>}
    </div><label className="network-choice"><input type="checkbox" checked={usePassword} onChange={(event) => setUsePassword(event.target.checked)} />Use a sudo password if required</label></fieldset>
    <button className="btn btn-sm" disabled={busy || plan !== null || !points.some((connection) => connection.id === connectionId && !connection.readOnly)} onClick={() => void prepare()}>Review remote action</button>
    {plan === null ? null : <div role="alertdialog" aria-label="Confirm remote diagnostic action" className="notice notice-caution">
      <strong>{plan.serverName}</strong><p>{plan.requiresSudo ? 'Elevated privileges required.' : 'Runs as the connected user.'} {plan.warning}</p>
      {plan.packages.length === 0 ? null : <p>Packages: {plan.packages.join(', ')}</p>}<pre className="network-command">{plan.command}</pre>
      {plan.requiresSudo && usePassword ? <label className="field"><span className="field-label">Sudo password</span><input className="input" type="password" autoComplete="off" value={password} onChange={(event) => setPassword(event.target.value)} /></label> : null}
      <div className="network-choices"><button className="btn btn-safe" autoFocus disabled={busy} onClick={() => { setPlan(null); setPassword(''); }}>Cancel action</button><button className="btn" disabled={busy || (plan.requiresSudo && usePassword && password === '')} onClick={() => void execute()}>{busy ? 'Running…' : 'Confirm remote action'}</button></div>
    </div>}
    {error === null ? null : <p className="notice notice-danger" role="alert">{error}</p>}
    {result === null ? null : <div role="status"><p>{result.outcome.status}: {result.outcome.detail}</p><pre className="network-command">{result.capabilities}</pre></div>}
  </section>;
}

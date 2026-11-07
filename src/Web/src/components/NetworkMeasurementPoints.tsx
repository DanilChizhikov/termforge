import { useState } from 'react';
import type { DiagnosticPoint } from '../api/networkDiagnostics';
import { useConnectionsSnapshot } from '../state/connections';
import { updateDiagnosticDraft, useNetworkDiagnostics } from '../state/networkDiagnostics';
import { NetworkDiagnosticDialog } from './NetworkDiagnosticDialog';
import { CloseIcon } from './icons/Icons';

export function NetworkMeasurementPoints({ disabled }: { disabled: boolean }) {
  const { draft } = useNetworkDiagnostics();
  const connections = useConnectionsSnapshot();
  const [selection, setSelection] = useState<DiagnosticPoint[] | null>(null);
  const [query, setQuery] = useState('');
  const points = [{ id: null, name: 'Local machine', host: 'This computer', groupName: '' }, ...connections];
  const normalized = query.trim().toLocaleLowerCase();
  const matches = points.filter((point) => `${point.name} ${point.host} ${point.groupName ?? ''}`.toLocaleLowerCase().includes(normalized));

  function close() { setSelection(null); }

  return (
    <section aria-label="Measurement points" className="network-settings-section">
      <div className="network-section-header"><h3 className="section-label">Measurement points</h3><span className="field-hint">{draft.points.length} / 8</span></div>
      <fieldset disabled={disabled} className="network-fields">
        <div className="network-selected-points">
          {draft.points.map((selected) => {
            const point = points.find((item) => item.id === selected.connectionId);
            const name = point?.name ?? 'Unavailable server';
            return <div key={selected.connectionId ?? 'local'} className="network-point">
              <div className="network-section-header">
                <div className="network-point-identity"><span>{name}</span><code className="field-hint">{point?.host ?? selected.connectionId}</code></div>
                <button type="button" className="icon-btn" aria-label={`Remove ${name}`} onClick={() => updateDiagnosticDraft({ points: draft.points.filter((item) => item.connectionId !== selected.connectionId) })}><CloseIcon /></button>
              </div>
              <label className="field network-point-category"><span className="visually-hidden">Category for {name}</span><select className="input" value={selected.category} onChange={(event) => updateDiagnosticDraft({ points: draft.points.map((item) => item.connectionId === selected.connectionId ? { ...item, category: event.target.value as DiagnosticPoint['category'] } : item) })}><option value="rf">Russia</option><option value="outsideRf">Outside Russia</option></select></label>
            </div>;
          })}
        </div>
        <button type="button" className="btn btn-sm" onClick={() => { setQuery(''); setSelection(draft.points.map((point) => ({ ...point }))); }}>Choose measurement points…</button>
      </fieldset>
      {selection === null ? null : <NetworkDiagnosticDialog title="Measurement points" onClose={close}>
        <div className="dialog-body network-picker-body">
          <p className="field-hint">Choose up to eight points, including this machine.</p>
          <label className="field"><span className="field-label">Search servers</span><input className="input" type="search" value={query} placeholder="Name, host or group" onChange={(event) => setQuery(event.target.value)} /></label>
          <div className="network-section-header"><span className="field-hint">{matches.length} of {points.length} points</span><span role="status" className="field-hint">{selection.length} / 8 selected{selection.length >= 8 ? ' · Limit reached' : ''}</span></div>
          <div className="network-point-list">
            {matches.length === 0 ? <p className="field-hint">No matching points.</p> : matches.map((point) => {
              const checked = selection.some((item) => item.connectionId === point.id);
              return <label key={point.id ?? 'local'} className="network-picker-point">
                <input type="checkbox" className="checkbox" aria-label={point.name} checked={checked} disabled={!checked && selection.length >= 8} onChange={(event) => setSelection(event.target.checked ? [...selection, { connectionId: point.id, category: 'rf' }] : selection.filter((item) => item.connectionId !== point.id))} />
                <span className="network-point-identity"><span>{point.name}</span><span className="field-hint"><code>{point.host}</code>{point.groupName ? ` · ${point.groupName}` : ''}</span></span>
              </label>;
            })}
          </div>
        </div>
        <div className="dialog-footer"><button type="button" className="btn" onClick={close}>Cancel</button><button type="button" className="btn btn-primary" onClick={() => { updateDiagnosticDraft({ points: selection }); close(); }}>Apply selection</button></div>
      </NetworkDiagnosticDialog>}
    </section>
  );
}

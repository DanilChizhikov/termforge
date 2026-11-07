import { useSyncExternalStore } from 'react';
import { describeError } from '../api/connections';
import {
  cancelDiagnostic, getDiagnostic, getDiagnosticResults, prepareDiagnostic, startDiagnostic,
  type DiagnosticRequest, type DiagnosticResult, type DiagnosticSnapshot,
} from '../api/networkDiagnostics';
import { activateWorkspaceTab } from './workspaceTab';
import { showAppView } from './appView';

export interface DiagnosticDraft extends DiagnosticRequest { targetConnectionId: string; host: string; domain: string; url: string }
interface NetworkState {
  draft: DiagnosticDraft;
  run: DiagnosticSnapshot | null;
  results: DiagnosticResult[];
  offset: number;
  busy: boolean;
  error: string | null;
  pending: DiagnosticRequest | null;
}

function initialState(): NetworkState {
  return {
    draft: { targetConnectionId: '', host: '', domain: '', url: '', ports: '22,80,443', allPorts: false,
      protocols: ['dns', 'tcp', 'ssh', 'tls', 'http', 'route'], points: [{ connectionId: null, category: 'rf' }] },
    run: null, results: [], offset: 0, busy: false, error: null, pending: null,
  };
}
let state = initialState();
let listeners: Array<() => void> = [];
let refreshVersion = 0;
const subscribe = (listener: () => void) => {
  listeners = [...listeners, listener];
  return () => { listeners = listeners.filter((item) => item !== listener); };
};
function update(next: Partial<NetworkState>) {
  state = { ...state, ...next };
  listeners.forEach((listener) => listener());
}
export const useNetworkDiagnostics = () => useSyncExternalStore(subscribe, () => state, () => state);
export const getNetworkDiagnostics = () => state;
export const updateDiagnosticDraft = (patch: Partial<DiagnosticDraft>) => update({ draft: { ...state.draft, ...patch }, pending: null });
export const dismissDiagnosticConfirmation = () => update({ pending: null });
export const resetNetworkDiagnostics = () => { refreshVersion++; state = initialState(); listeners.forEach((listener) => listener()); };

export function openNetworkDiagnostics(connectionId: string) {
  updateDiagnosticDraft({ targetConnectionId: connectionId });
  showAppView('workspace');
  activateWorkspaceTab('diagnostics');
}

export async function runNetworkDiagnostic(confirmed = false) {
  if (state.busy || state.run?.status === 'running' || state.run?.status === 'queued') return;
  const draft = state.draft;
  const request: DiagnosticRequest = confirmed && state.pending !== null ? state.pending : {
    ...(draft.targetConnectionId === '' ? (draft.host.trim() === '' ? {} : { host: draft.host }) : { targetConnectionId: draft.targetConnectionId }),
    ...(draft.domain.trim() === '' ? {} : { domain: draft.domain }),
    ...(draft.url.trim() === '' ? {} : { url: draft.url }),
    ports: draft.ports, allPorts: draft.allPorts, protocols: draft.protocols, points: draft.points,
  };
  update({ busy: true, error: null });
  try {
    if (!confirmed) {
      const prepared = await prepareDiagnostic(request);
      if (prepared.confirmation !== null) {
        update({ pending: { ...request, confirmationToken: prepared.confirmation.token } });
        return;
      }
    }
    const run = await startDiagnostic(request);
    refreshVersion++;
    update({ run, results: [], offset: 0, pending: null });
    await refreshDiagnostic();
  } catch (error) { update({ error: describeError(error), pending: null }); }
  finally { update({ busy: false }); }
}

export async function refreshDiagnostic() {
  const run = state.run;
  if (run === null) return;
  const offset = state.offset;
  const version = ++refreshVersion;
  try {
    const [snapshot, results] = await Promise.all([getDiagnostic(run.id), getDiagnosticResults(run.id, offset)]);
    if (version === refreshVersion && state.run?.id === run.id && offset === state.offset) {
      update({ run: snapshot, results, error: null });
    }
  } catch (error) {
    if (version === refreshVersion && state.run?.id === run.id) update({ error: describeError(error) });
  }
}

export function receiveDiagnosticSnapshot(snapshot: DiagnosticSnapshot) {
  if (state.run !== null && snapshot.id === state.run.id && snapshot.completed >= state.run.completed) {
    update({ run: snapshot });
    void refreshDiagnostic();
  }
}

export async function changeDiagnosticPage(offset: number) {
  update({ offset: Math.max(0, offset), results: [] });
  await refreshDiagnostic();
}

export async function stopNetworkDiagnostic() {
  if (state.run === null || state.busy) return;
  update({ busy: true });
  try { await cancelDiagnostic(state.run.id); await refreshDiagnostic(); }
  catch (error) { update({ error: describeError(error) }); }
  finally { update({ busy: false }); }
}

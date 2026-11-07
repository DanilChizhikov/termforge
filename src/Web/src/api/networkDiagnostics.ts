import { apiClient } from './client';

export const NETWORK_PROTOCOLS = ['dns', 'tcp', 'udp', 'ssh', 'tls', 'http', 'route'] as const;
export type NetworkProtocol = typeof NETWORK_PROTOCOLS[number];
export interface DiagnosticPoint { connectionId: string | null; category: 'rf' | 'outsideRf' }
export interface DiagnosticRequest {
  targetConnectionId?: string;
  host?: string;
  domain?: string;
  url?: string;
  ports: string;
  allPorts: boolean;
  protocols: NetworkProtocol[];
  points: DiagnosticPoint[];
  confirmationToken?: string;
}
export interface DiagnosticSnapshot {
  id: string;
  host: string;
  status: 'queued' | 'running' | 'completed' | 'failed' | 'cancelled';
  completed: number;
  total: number;
  points: DiagnosticPoint[];
  error: string | null;
}
export interface ProbeOutcome {
  status: 'responded' | 'refused' | 'noResponse' | 'error' | 'unavailable';
  detail: string;
}
export interface DiagnosticResult {
  sequence: number;
  point: DiagnosticPoint;
  pointName: string;
  protocol: NetworkProtocol;
  port: number | null;
  outcome: ProbeOutcome;
  elapsedMilliseconds: number;
}
export interface DiagnosticPreparation {
  plan: { host: string; ports: number[]; fullScan: boolean; probesPerPoint: number; points: DiagnosticPoint[] };
  confirmation: { token: string; expiresAt: string } | null;
}
export interface RemoteActionRequest {
  connectionId: string;
  kind: 'install' | 'udpDetails';
  protocols: NetworkProtocol[];
  host?: string;
  port?: number;
  usePassword: boolean;
}
export interface RemoteActionPlan {
  id: string;
  connectionId: string;
  serverName: string;
  kind: string;
  packages: string[];
  command: string;
  requiresSudo: boolean;
  confirmationToken: string;
  expiresAt: string;
  warning: string;
}
export interface RemoteActionResult { outcome: ProbeOutcome; capabilities: string }

const PREFIX = '/api/network-diagnostics';
export const prepareDiagnostic = (request: DiagnosticRequest) => apiClient.post<DiagnosticPreparation>(`${PREFIX}/prepare`, request);
export const startDiagnostic = (request: DiagnosticRequest) => apiClient.post<DiagnosticSnapshot>(`${PREFIX}/`, request);
export const getDiagnostic = (id: string) => apiClient.get<DiagnosticSnapshot>(`${PREFIX}/${encodeURIComponent(id)}`);
export const getDiagnosticResults = (id: string, offset: number) => apiClient.get<DiagnosticResult[]>(`${PREFIX}/${encodeURIComponent(id)}/results?offset=${offset}&limit=100`);
export const cancelDiagnostic = (id: string) => apiClient.post<void>(`${PREFIX}/${encodeURIComponent(id)}/cancel`);
export const prepareRemoteAction = (request: RemoteActionRequest) => apiClient.post<RemoteActionPlan>(`${PREFIX}/remote-actions/prepare`, request);
export const executeRemoteAction = (plan: RemoteActionPlan, password: string) => apiClient.post<RemoteActionResult>(`${PREFIX}/remote-actions/execute`, {
  planId: plan.id, confirmationToken: plan.confirmationToken, ...(password === '' ? {} : { password }),
});

import { apiClient, type ApiClient } from './client';

export interface RecordingSettings {
  enabled: boolean;
  maxRecordingSizeMegabytes: number;
  maxRecordings: number;
  retentionDays: number;
}

export interface RecordingSettingsResponse extends RecordingSettings {
  directory: string;
}

export type RecordingStatus = 'recording' | 'complete' | 'truncated' | 'interrupted';

export type RecordingEndReason =
  | 'stopped'
  | 'sessionClosed'
  | 'recordingDisabled'
  | 'sizeLimitReached'
  | 'writerFellBehind'
  | 'storageFailed';

export interface RecordingSummary {
  id: string;
  connectionId: string;
  connectionName: string;
  sessionNumber: number;
  columns: number;
  rows: number;
  startedAt: string;
  endedAt: string | null;
  sizeBytes: number;
  status: RecordingStatus;
  endReason: RecordingEndReason | null;
}

export interface SessionRecording {
  recording: boolean;
  available: boolean;
  recordingId: string | null;
  sizeBytes: number;
  maxBytes: number;
  status: RecordingStatus | null;
  endReason: RecordingEndReason | null;
}

export interface CastEvent {
  time: number;
  data: string;
}

export interface Cast {
  width: number;
  height: number;
  events: CastEvent[];
}

export const RECORDING_LIMITS = {
  minSizeMegabytes: 1,
  maxSizeMegabytes: 100,
  minRecordings: 1,
  maxRecordings: 500,
  minRetentionDays: 1,
  maxRetentionDays: 365,
} as const;

const RECORDINGS_PATH = '/api/recordings';
const SETTINGS_PATH = `${RECORDINGS_PATH}/settings`;

function sessionRecordingPath(terminalId: string): string {
  return `/api/terminals/${encodeURIComponent(terminalId)}/recording`;
}

export function getRecordingSettings(client: ApiClient = apiClient): Promise<RecordingSettingsResponse> {
  return client.get<RecordingSettingsResponse>(SETTINGS_PATH);
}

export function updateRecordingSettings(
  settings: RecordingSettings,
  client: ApiClient = apiClient,
): Promise<RecordingSettingsResponse> {
  return client.put<RecordingSettingsResponse>(SETTINGS_PATH, settings);
}

export function listRecordings(client: ApiClient = apiClient): Promise<RecordingSummary[]> {
  return client.get<RecordingSummary[]>(RECORDINGS_PATH);
}

export function deleteRecording(id: string, client: ApiClient = apiClient): Promise<void> {
  return client.delete<void>(`${RECORDINGS_PATH}/${encodeURIComponent(id)}`);
}

export async function fetchRecordingCast(id: string, signal: AbortSignal, client: ApiClient = apiClient): Promise<Cast> {
  const response = await client.openStream(`${RECORDINGS_PATH}/${encodeURIComponent(id)}/cast`, signal);
  const text = await response.text();
  const cast = parseCast(text);
  if (cast === null) {
    throw new Error('The recording is not a valid asciicast v2 file.');
  }
  return cast;
}

export function getSessionRecording(terminalId: string, client: ApiClient = apiClient): Promise<SessionRecording> {
  return client.get<SessionRecording>(sessionRecordingPath(terminalId));
}

export function startSessionRecording(terminalId: string, client: ApiClient = apiClient): Promise<SessionRecording> {
  return client.post<SessionRecording>(sessionRecordingPath(terminalId));
}

export function stopSessionRecording(terminalId: string, client: ApiClient = apiClient): Promise<SessionRecording> {
  return client.delete<SessionRecording>(sessionRecordingPath(terminalId));
}

export function parseCast(text: string): Cast | null {
  const lines = text.split('\n').filter((line) => line.trim().length > 0);
  const headerLine = lines[0];
  if (headerLine === undefined) {
    return null;
  }
  const header = parseJson(headerLine);
  if (!isRecord(header) || header['version'] !== 2) {
    return null;
  }
  const width = header['width'];
  const height = header['height'];
  if (!isPositiveInteger(width) || !isPositiveInteger(height)) {
    return null;
  }
  const events: CastEvent[] = [];
  for (const line of lines.slice(1)) {
    const event = parseOutputEvent(line);
    if (event !== null) {
      events.push(event);
    }
  }
  return { width, height, events };
}

function parseOutputEvent(line: string): CastEvent | null {
  const parsed = parseJson(line);
  if (!Array.isArray(parsed) || parsed.length !== 3) {
    return null;
  }
  const [time, type, data] = parsed as unknown[];
  if (typeof time !== 'number' || type !== 'o' || typeof data !== 'string') {
    return null;
  }
  return { time, data };
}

function parseJson(text: string): unknown {
  try {
    return JSON.parse(text) as unknown;
  } catch {
    return null;
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function isPositiveInteger(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value > 0;
}

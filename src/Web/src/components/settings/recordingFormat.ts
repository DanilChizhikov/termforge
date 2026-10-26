import type { RecordingStatus, RecordingSummary } from '../../api/recordings';
import { formatBytes } from '../metricsFormat';

export interface RecordingStatusView {
  label: string;
  dotClassName: string;
  textClassName: string;
}

const STATUS_VIEWS: Record<RecordingStatus, RecordingStatusView> = {
  recording: { label: 'Recording', dotClassName: 'status-dot-active', textClassName: 'status-text-active' },
  complete: { label: 'Complete', dotClassName: 'status-dot-ok', textClassName: 'status-text-ok' },
  truncated: { label: 'Truncated', dotClassName: 'status-dot-caution', textClassName: 'status-text-caution' },
  interrupted: { label: 'Interrupted', dotClassName: 'status-dot-idle', textClassName: 'status-text-idle' },
};

const SECONDS_PER_MINUTE = 60;
const MILLISECONDS_PER_SECOND = 1000;

export function recordingStatusView(status: RecordingStatus): RecordingStatusView {
  return STATUS_VIEWS[status];
}

export function formatRecordingSize(bytes: number): string {
  const size = formatBytes(bytes);
  return `${size.amount} ${size.unit}`;
}

export function formatDuration(totalSeconds: number): string {
  const seconds = Math.max(0, Math.round(totalSeconds));
  const minutes = Math.floor(seconds / SECONDS_PER_MINUTE);
  const remainder = seconds % SECONDS_PER_MINUTE;
  return `${minutes}:${String(remainder).padStart(2, '0')}`;
}

export function recordingDuration(recording: RecordingSummary): string | null {
  if (recording.endedAt === null) {
    return null;
  }
  const elapsed = (Date.parse(recording.endedAt) - Date.parse(recording.startedAt)) / MILLISECONDS_PER_SECOND;
  return formatDuration(elapsed);
}

export function recordingTitle(recording: RecordingSummary): string {
  return `${recording.connectionName} #${recording.sessionNumber}`;
}

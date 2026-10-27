import { useEffect, useMemo, useRef, useState } from 'react';
import { containerLogsUrl, LOG_TAIL_OPTIONS, parseLogMessage } from '../../api/docker';
import { CloseIcon, SearchIcon, TerminalIcon } from '../icons/Icons';

export interface ContainerLogsProps {
  connectionId: string;
  container: string;
  canOpenShell: boolean;
  onOpenShell: () => void;
  onClose: () => void;
}

type StreamState =
  | { kind: 'connecting' }
  | { kind: 'streaming' }
  | { kind: 'ended'; exitCode: number | null }
  | { kind: 'failed'; message: string };

const MAX_BUFFERED_CHARACTERS = 1_000_000;
const DEFAULT_TAIL = 200;
const CONNECTION_LOST = 'The log stream was interrupted.';

export function ContainerLogs({ connectionId, container, canOpenShell, onOpenShell, onClose }: ContainerLogsProps) {
  const [follow, setFollow] = useState(true);
  const [tail, setTail] = useState(DEFAULT_TAIL);
  const [filter, setFilter] = useState('');

  return (
    <section aria-label={`Logs of ${container}`} className="docker-panel">
      <div className="docker-panel-bar">
        <span className="docker-panel-title">Logs · {container}</span>
        <label className="check-row docker-inline-control">
          <input type="checkbox" className="checkbox" checked={follow} onChange={(event) => setFollow(event.target.checked)} />
          Follow
        </label>
        <label className="docker-inline-control">
          Last
          <select
            className="input input-sm docker-select"
            value={tail}
            onChange={(event) => setTail(Number(event.target.value))}
          >
            {LOG_TAIL_OPTIONS.map((option) => (
              <option key={option} value={option}>
                {option} lines
              </option>
            ))}
          </select>
        </label>
        <span className="docker-filter">
          <SearchIcon size={13} />
          <input
            type="search"
            className="docker-filter-input"
            placeholder="Filter lines"
            aria-label="Filter log lines"
            value={filter}
            onChange={(event) => setFilter(event.target.value)}
          />
        </span>
        <span className="docker-panel-spacer" />
        {canOpenShell ? (
          <button type="button" className="btn btn-sm btn-primary" onClick={onOpenShell}>
            <TerminalIcon size={13} />
            Open shell in container
          </button>
        ) : null}
        <button type="button" className="icon-btn" aria-label="Close logs" onClick={onClose}>
          <CloseIcon />
        </button>
      </div>
      <LogStream
        key={`${tail}:${String(follow)}`}
        connectionId={connectionId}
        container={container}
        tail={tail}
        follow={follow}
        filter={filter}
      />
    </section>
  );
}

interface LogStreamProps {
  connectionId: string;
  container: string;
  tail: number;
  follow: boolean;
  filter: string;
}

function LogStream({ connectionId, container, tail, follow, filter }: LogStreamProps) {
  const [text, setText] = useState('');
  const [stream, setStream] = useState<StreamState>({ kind: 'connecting' });
  const outputRef = useRef<HTMLPreElement>(null);

  useEffect(() => {
    let finished = false;
    const socket = new WebSocket(containerLogsUrl(connectionId, container, { tail, follow }));
    socket.onopen = () => setStream({ kind: 'streaming' });
    socket.onmessage = (event: MessageEvent) => {
      if (typeof event.data !== 'string') {
        return;
      }
      const message = parseLogMessage(event.data);
      if (message === null) {
        return;
      }
      if (message.type === 'output') {
        setText((previous) => appendBounded(previous, message.text));
        return;
      }
      finished = true;
      setStream(
        message.type === 'end' ? { kind: 'ended', exitCode: message.exitCode } : { kind: 'failed', message: message.message },
      );
    };
    socket.onclose = () => {
      if (!finished) {
        finished = true;
        setStream({ kind: 'failed', message: CONNECTION_LOST });
      }
    };
    return () => {
      finished = true;
      socket.close();
    };
  }, [connectionId, container, tail, follow]);

  useEffect(() => {
    const output = outputRef.current;
    if (output !== null && follow) {
      output.scrollTop = output.scrollHeight;
    }
  }, [text, follow]);

  const visibleText = useMemo(() => filterLines(text, filter), [text, filter]);

  return (
    <>
      <pre ref={outputRef} className="docker-log-output" aria-label="Log output" tabIndex={0}>
        {visibleText.split('\n').map((line, index) => (
          <LogLine key={index} line={line} last={index === lineCount(visibleText) - 1} />
        ))}
      </pre>
      <div className="docker-panel-footer">
        <StreamStatus stream={stream} />
      </div>
    </>
  );
}

function StreamStatus({ stream }: { stream: StreamState }) {
  if (stream.kind === 'failed') {
    return (
      <span role="alert" className="status-text status-text-danger">
        <span className="status-dot status-dot-sm status-dot-danger" aria-hidden="true" />
        {stream.message}
      </span>
    );
  }
  if (stream.kind === 'ended') {
    const code = stream.exitCode === null ? '' : ` (exit code ${stream.exitCode})`;
    return (
      <span className="status-text status-text-idle">
        <span className="status-dot status-dot-sm status-dot-idle" aria-hidden="true" />
        Stream ended{code}
      </span>
    );
  }
  const label = stream.kind === 'connecting' ? 'Connecting' : 'Streaming';
  return (
    <span className="status-text status-text-active">
      <span className="status-dot status-dot-sm status-dot-active" aria-hidden="true" />
      {label}
    </span>
  );
}

const TIMESTAMP_PATTERN = /^(\S*\d{2}:\d{2}:\d{2}\S*)(\s.*)?$/;
const ERROR_PATTERN = /\b(ERROR|ERR|FATAL|CRIT(?:ICAL)?)\b/;
const WARNING_PATTERN = /\bWARN(?:ING)?\b/;

function lineCount(text: string): number {
  return text.split('\n').length;
}

function levelClass(text: string): string | undefined {
  if (ERROR_PATTERN.test(text)) {
    return 'docker-log-error';
  }
  return WARNING_PATTERN.test(text) ? 'docker-log-warning' : undefined;
}

function LogLine({ line, last }: { line: string; last: boolean }) {
  const ending = last ? '' : '\n';
  const timestamp = TIMESTAMP_PATTERN.exec(line);
  if (timestamp === null) {
    return (
      <span className={levelClass(line)}>
        {line}
        {ending}
      </span>
    );
  }
  const rest = timestamp[2] ?? '';
  return (
    <span>
      <span className="docker-log-time">{timestamp[1]}</span>
      <span className={levelClass(rest)}>{rest}</span>
      {ending}
    </span>
  );
}

function appendBounded(previous: string, addition: string): string {
  const combined = previous + addition;
  if (combined.length <= MAX_BUFFERED_CHARACTERS) {
    return combined;
  }
  const trimmed = combined.slice(combined.length - MAX_BUFFERED_CHARACTERS);
  const firstLineBreak = trimmed.indexOf('\n');
  return firstLineBreak < 0 ? trimmed : trimmed.slice(firstLineBreak + 1);
}

function filterLines(text: string, filter: string): string {
  const needle = filter.trim().toLowerCase();
  if (needle === '') {
    return text;
  }
  return text
    .split('\n')
    .filter((line) => line.toLowerCase().includes(needle))
    .join('\n');
}

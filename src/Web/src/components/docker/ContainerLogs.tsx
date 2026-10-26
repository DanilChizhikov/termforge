import { useEffect, useMemo, useRef, useState } from 'react';
import { containerLogsUrl, LOG_TAIL_OPTIONS, parseLogMessage } from '../../api/docker';
import { CloseIcon } from '../icons/Icons';

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
        <span className="docker-panel-title">
          Logs · <code>{container}</code>
        </span>
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
        <input
          type="search"
          className="input input-sm docker-filter"
          placeholder="Filter lines"
          aria-label="Filter log lines"
          value={filter}
          onChange={(event) => setFilter(event.target.value)}
        />
        <span className="docker-panel-spacer" />
        {canOpenShell ? (
          <button type="button" className="btn btn-sm btn-primary" onClick={onOpenShell}>
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
        {visibleText}
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

import { useEffect, useEffectEvent, useRef, useState } from 'react';
import { imagePullUrl, parseLogMessage } from '../../api/docker';
import { CloseIcon } from '../icons/Icons';

export interface ImagePullProgressProps {
  connectionId: string;
  image: string;
  onFinished: (succeeded: boolean) => void;
  onClose: () => void;
}

type PullState =
  | { kind: 'connecting' }
  | { kind: 'pulling' }
  | { kind: 'ended'; exitCode: number | null }
  | { kind: 'failed'; message: string };

const MAX_BUFFERED_CHARACTERS = 200_000;
const CONNECTION_LOST = 'The pull stream was interrupted. Check the journal for the result.';

export function ImagePullProgress({ connectionId, image, onFinished, onClose }: ImagePullProgressProps) {
  const [text, setText] = useState('');
  const [state, setState] = useState<PullState>({ kind: 'connecting' });
  const outputRef = useRef<HTMLPreElement>(null);
  const notifyFinished = useEffectEvent((succeeded: boolean) => onFinished(succeeded));

  useEffect(() => {
    let finished = false;
    const socket = new WebSocket(imagePullUrl(connectionId, image));
    socket.onopen = () => setState({ kind: 'pulling' });
    socket.onmessage = (event: MessageEvent) => {
      if (typeof event.data !== 'string') {
        return;
      }
      const message = parseLogMessage(event.data);
      if (message === null) {
        return;
      }
      if (message.type === 'output') {
        setText((previous) => (previous + message.text).slice(-MAX_BUFFERED_CHARACTERS));
        return;
      }
      finished = true;
      if (message.type === 'end') {
        setState({ kind: 'ended', exitCode: message.exitCode });
        notifyFinished(message.exitCode === 0);
        return;
      }
      setState({ kind: 'failed', message: message.message });
      notifyFinished(false);
    };
    socket.onclose = () => {
      if (!finished) {
        finished = true;
        setState({ kind: 'failed', message: CONNECTION_LOST });
        notifyFinished(false);
      }
    };
    return () => {
      finished = true;
      socket.close();
    };
  }, [connectionId, image]);

  useEffect(() => {
    const output = outputRef.current;
    if (output !== null) {
      output.scrollTop = output.scrollHeight;
    }
  }, [text]);

  const isActive = state.kind === 'connecting' || state.kind === 'pulling';

  return (
    <section aria-label={`Pull of ${image}`} className="docker-panel docker-pull-panel">
      <div className="docker-panel-bar">
        <span className="docker-panel-title">
          Pull · <code>{image}</code>
        </span>
        <span className="docker-panel-spacer" />
        <button type="button" className="icon-btn" aria-label="Close pull output" disabled={isActive} onClick={onClose}>
          <CloseIcon />
        </button>
      </div>
      <pre ref={outputRef} className="docker-log-output" aria-label="Pull output" tabIndex={0}>
        {text}
      </pre>
      <div className="docker-panel-footer">
        <PullStatus state={state} />
      </div>
    </section>
  );
}

function PullStatus({ state }: { state: PullState }) {
  if (state.kind === 'failed') {
    return (
      <span role="alert" className="status-text status-text-danger">
        <span className="status-dot status-dot-sm status-dot-danger" aria-hidden="true" />
        {state.message}
      </span>
    );
  }
  if (state.kind === 'ended') {
    const succeeded = state.exitCode === 0;
    const code = state.exitCode === null ? '' : ` (exit code ${state.exitCode})`;
    return (
      <span role={succeeded ? 'status' : 'alert'} className={succeeded ? 'status-text status-text-ok' : 'status-text status-text-danger'}>
        <span className={succeeded ? 'status-dot status-dot-sm status-dot-ok' : 'status-dot status-dot-sm status-dot-danger'} aria-hidden="true" />
        {succeeded ? 'Pulled. Recorded in the journal.' : `Pull failed${code}`}
      </span>
    );
  }
  const label = state.kind === 'connecting' ? 'Connecting' : 'Pulling';
  return (
    <span className="status-text status-text-active">
      <span className="status-dot status-dot-sm status-dot-active" aria-hidden="true" />
      {label}
    </span>
  );
}

import { Terminal } from '@xterm/xterm';
import '@xterm/xterm/css/xterm.css';
import { useEffect, useRef, useState } from 'react';
import { ApiError } from '../../api/client';
import { describeError } from '../../api/connections';
import { fetchRecordingCast, type RecordingSummary } from '../../api/recordings';
import { CastPlayback, IDLE_LIMIT_SECONDS, type PlaybackState } from '../../state/castPlayback';
import { PauseIcon, PlayIcon, RefreshIcon } from '../icons/Icons';
import {
  TERMINAL_CURSOR_STYLE,
  TERMINAL_FONT_FAMILY,
  TERMINAL_FONT_SIZE,
  TERMINAL_LINE_HEIGHT,
  createTerminalTheme,
} from '../terminalTheme';
import { formatDuration, recordingTitle } from './recordingFormat';

export interface RecordingPlayerProps {
  recording: RecordingSummary;
  onClose: () => void;
}

const STATE_LABELS: Record<PlaybackState, string> = {
  playing: 'Playing',
  paused: 'Paused',
  finished: 'Finished',
};

export function RecordingPlayer({ recording, onClose }: RecordingPlayerProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const playbackRef = useRef<CastPlayback | null>(null);
  const [state, setState] = useState<PlaybackState | null>(null);
  const [duration, setDuration] = useState(0);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const container = containerRef.current;
    if (container === null) {
      return;
    }
    const abort = new AbortController();
    let terminal: Terminal | null = null;
    fetchRecordingCast(recording.id, abort.signal)
      .then((cast) => {
        if (abort.signal.aborted) {
          return;
        }
        const player = new Terminal({
          cols: cast.width,
          rows: cast.height,
          disableStdin: true,
          cursorBlink: false,
          fontFamily: TERMINAL_FONT_FAMILY,
          fontSize: TERMINAL_FONT_SIZE,
          lineHeight: TERMINAL_LINE_HEIGHT,
          cursorStyle: TERMINAL_CURSOR_STYLE,
          cursorInactiveStyle: TERMINAL_CURSOR_STYLE,
          theme: createTerminalTheme(),
        });
        player.open(container);
        terminal = player;
        const playback = new CastPlayback(cast.events, {
          write: (data) => player.write(data),
          reset: () => player.reset(),
          onStateChange: setState,
        });
        playbackRef.current = playback;
        setDuration(playback.durationSeconds);
        playback.play();
      })
      .catch((failure: unknown) => {
        if (!abort.signal.aborted) {
          setError(failure instanceof ApiError || !(failure instanceof Error) ? describeError(failure) : failure.message);
        }
      });
    return () => {
      abort.abort();
      playbackRef.current?.dispose();
      playbackRef.current = null;
      terminal?.dispose();
    };
  }, [recording.id]);

  const togglePlayback = () => {
    const playback = playbackRef.current;
    if (playback === null) {
      return;
    }
    if (playback.state === 'playing') {
      playback.pause();
    } else {
      playback.play();
    }
  };

  const title = recordingTitle(recording);

  return (
    <div className="dialog-backdrop">
      <div role="dialog" aria-modal="true" aria-labelledby="recording-player-title" className="dialog-card recording-player">
        <div className="dialog-header">
          <h2 id="recording-player-title" className="dialog-title">
            Replay <code>{title}</code>
          </h2>
          <p className="text-secondary">
            {new Date(recording.startedAt).toLocaleString()} · pauses longer than {IDLE_LIMIT_SECONDS} s are shortened
          </p>
        </div>
        <div className="dialog-body">
          {error === null ? null : (
            <p role="alert" className="form-error">
              {error}
            </p>
          )}
          <div ref={containerRef} className="recording-player-screen" data-testid="recording-player-screen" />
        </div>
        <div className="dialog-footer">
          <span className="text-secondary recording-player-state" aria-live="polite">
            {state === null ? 'Loading…' : `${STATE_LABELS[state]} · ${formatDuration(duration)}`}
          </span>
          <span className="dialog-footer-spacer" />
          <button type="button" className="btn btn-sm" disabled={state === null} onClick={togglePlayback}>
            {state === 'playing' ? <PauseIcon size={13} /> : <PlayIcon size={13} />}
            {state === 'playing' ? 'Pause' : 'Play'}
          </button>
          <button
            type="button"
            className="btn btn-sm"
            disabled={state === null}
            onClick={() => playbackRef.current?.restart()}
          >
            <RefreshIcon size={13} />
            Restart
          </button>
          <button type="button" className="btn btn-sm btn-safe" autoFocus onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
}

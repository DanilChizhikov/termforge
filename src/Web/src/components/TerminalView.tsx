import { FitAddon } from '@xterm/addon-fit';
import { Terminal } from '@xterm/xterm';
import '@xterm/xterm/css/xterm.css';
import { useEffect, useRef } from 'react';
import { describeError } from '../api/connections';
import { TERMINAL_FONT_FAMILY, TERMINAL_FONT_SIZE, TERMINAL_LINE_HEIGHT, TERMINAL_THEME } from './terminalTheme';
import {
  closeTerminal,
  openTerminal,
  parseServerMessage,
  resizeMessage,
  terminalSocketUrl,
  type TerminalSession,
  type TerminalSize,
  type TerminalState,
} from '../api/terminals';

export interface TerminalViewProps {
  connectionId: string;
  active: boolean;
  onOpened: (session: TerminalSession) => void;
  onStateChange: (state: TerminalState, reason: string | null) => void;
}

const CONNECTION_LOST = 'The connection to TermForge was lost.';

interface TerminalCallbacks {
  onOpened: (session: TerminalSession) => void;
  onStateChange: (state: TerminalState, reason: string | null) => void;
}

export function TerminalView({ connectionId, active, onOpened, onStateChange }: TerminalViewProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const fitRef = useRef<(() => void) | null>(null);
  const callbacksRef = useRef<TerminalCallbacks>({ onOpened, onStateChange });

  useEffect(() => {
    callbacksRef.current = { onOpened, onStateChange };
  });

  useEffect(() => {
    const container = containerRef.current;
    if (container === null) {
      return;
    }
    const terminal = new Terminal({
      cursorBlink: true,
      fontFamily: TERMINAL_FONT_FAMILY,
      fontSize: TERMINAL_FONT_SIZE,
      lineHeight: TERMINAL_LINE_HEIGHT,
      theme: TERMINAL_THEME,
    });
    const fitAddon = new FitAddon();
    terminal.loadAddon(fitAddon);
    terminal.open(container);

    const encoder = new TextEncoder();
    let socket: WebSocket | null = null;
    let disposed = false;
    let finished = false;
    let currentSize: TerminalSize = { columns: terminal.cols, rows: terminal.rows };

    const report = (state: TerminalState, reason: string | null) => {
      if (finished || disposed) {
        return;
      }
      finished = state === 'disconnected';
      callbacksRef.current.onStateChange(state, reason);
    };

    const send = (data: string | Uint8Array<ArrayBuffer>) => {
      if (socket !== null && socket.readyState === WebSocket.OPEN) {
        socket.send(data);
      }
    };

    const fit = () => {
      if (container.clientWidth === 0 || container.clientHeight === 0) {
        return;
      }
      fitAddon.fit();
      const fitted = { columns: terminal.cols, rows: terminal.rows };
      if (fitted.columns === currentSize.columns && fitted.rows === currentSize.rows) {
        return;
      }
      currentSize = fitted;
      send(resizeMessage(fitted));
    };
    fitRef.current = fit;
    fit();

    const observer = new ResizeObserver(() => fit());
    observer.observe(container);
    const dataSubscription = terminal.onData((data) => send(encoder.encode(data)));
    const binarySubscription = terminal.onBinary((data) => {
      const bytes = Uint8Array.from(data, (character) => character.charCodeAt(0) & 0xff);
      send(bytes);
    });

    const attach = (session: TerminalSession) => {
      const webSocket = new WebSocket(terminalSocketUrl(session.id));
      webSocket.binaryType = 'arraybuffer';
      socket = webSocket;
      webSocket.onopen = () => {
        webSocket.send(resizeMessage(currentSize));
      };
      webSocket.onmessage = (event: MessageEvent) => {
        if (typeof event.data !== 'string') {
          terminal.write(new Uint8Array(event.data as ArrayBuffer));
          return;
        }
        const message = parseServerMessage(event.data);
        if (message === null) {
          return;
        }
        if (message.type === 'error') {
          terminal.write(`\r\n[TermForge] ${message.message}\r\n`);
        } else if (message.state === 'connected') {
          report('connected', null);
        } else {
          report('disconnected', message.reason ?? CONNECTION_LOST);
        }
      };
      webSocket.onclose = (event: CloseEvent) => {
        report('disconnected', event.reason !== '' ? event.reason : CONNECTION_LOST);
      };
    };

    openTerminal(connectionId, currentSize)
      .then((session) => {
        if (disposed) {
          closeTerminal(session.id).catch(() => undefined);
          return;
        }
        callbacksRef.current.onOpened(session);
        attach(session);
      })
      .catch((failure: unknown) => report('disconnected', describeError(failure)));

    return () => {
      disposed = true;
      fitRef.current = null;
      observer.disconnect();
      dataSubscription.dispose();
      binarySubscription.dispose();
      socket?.close();
      terminal.dispose();
    };
  }, [connectionId]);

  useEffect(() => {
    if (active) {
      fitRef.current?.();
    }
  }, [active]);

  return <div ref={containerRef} className="terminal-view" data-testid="terminal-view" />;
}

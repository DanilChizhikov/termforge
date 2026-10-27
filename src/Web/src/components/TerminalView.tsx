import { FitAddon } from '@xterm/addon-fit';
import { Terminal } from '@xterm/xterm';
import '@xterm/xterm/css/xterm.css';
import { useEffect, useImperativeHandle, useRef, type Ref } from 'react';
import { describeError } from '../api/connections';
import { TERMINAL_FONT_FAMILY, TERMINAL_FONT_SIZE, TERMINAL_LINE_HEIGHT, createTerminalTheme } from './terminalTheme';
import {
  closeTerminal,
  openTerminal,
  parseServerMessage,
  reconnectTerminal,
  resizeMessage,
  terminalSocketUrl,
  type TerminalSession,
  type TerminalSize,
  type TerminalState,
} from '../api/terminals';

export interface TerminalHandle {
  insertText: (text: string) => void;
}

export interface TerminalViewProps {
  connectionId: string;
  active: boolean;
  onOpened: (session: TerminalSession) => void;
  onStateChange: (state: TerminalState, reason: string | null) => void;
  generation?: number;
  reconnect?: boolean;
  handleRef?: Ref<TerminalHandle>;
  onHistoryChanged?: (() => void) | undefined;
  onResize?: ((size: TerminalSize) => void) | undefined;
  open?: ((size: TerminalSize) => Promise<TerminalSession>) | undefined;
}

const CONNECTION_LOST = 'The connection to TermForge was lost.';

interface TerminalCallbacks {
  onOpened: (session: TerminalSession) => void;
  onStateChange: (state: TerminalState, reason: string | null) => void;
  onHistoryChanged?: (() => void) | undefined;
  onResize?: ((size: TerminalSize) => void) | undefined;
}

export function TerminalView({
  connectionId,
  active,
  onOpened,
  onStateChange,
  generation = 0,
  reconnect = false,
  handleRef,
  onHistoryChanged,
  onResize,
  open,
}: TerminalViewProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const fitRef = useRef<(() => void) | null>(null);
  const terminalRef = useRef<Terminal | null>(null);
  const reconnectRef = useRef(reconnect);
  const openRef = useRef(open);
  const callbacksRef = useRef<TerminalCallbacks>({ onOpened, onStateChange, onHistoryChanged, onResize });

  useEffect(() => {
    callbacksRef.current = { onOpened, onStateChange, onHistoryChanged, onResize };
    reconnectRef.current = reconnect;
    openRef.current = open;
  });

  useImperativeHandle(
    handleRef,
    () => ({
      insertText: (text: string) => {
        terminalRef.current?.paste(text);
        terminalRef.current?.focus();
      },
    }),
    [],
  );

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
      theme: createTerminalTheme(),
    });
    const fitAddon = new FitAddon();
    terminal.loadAddon(fitAddon);
    terminal.open(container);
    terminalRef.current = terminal;

    const encoder = new TextEncoder();
    let socket: WebSocket | null = null;
    let disposed = false;
    let finished = false;
    let currentSize: TerminalSize = { columns: terminal.cols, rows: terminal.rows };
    callbacksRef.current.onResize?.(currentSize);

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
      callbacksRef.current.onResize?.(fitted);
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
        if (message.type === 'history') {
          callbacksRef.current.onHistoryChanged?.();
        } else if (message.type === 'error') {
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

    const launch = startSession(connectionId, currentSize, openRef.current, reconnectRef.current);
    launch
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
      terminalRef.current = null;
      observer.disconnect();
      dataSubscription.dispose();
      binarySubscription.dispose();
      socket?.close();
      terminal.dispose();
    };
  }, [connectionId, generation]);

  useEffect(() => {
    if (active) {
      fitRef.current?.();
    }
  }, [active]);

  return <div ref={containerRef} className="terminal-view" data-testid="terminal-view" />;
}

function startSession(
  connectionId: string,
  size: TerminalSize,
  open: ((size: TerminalSize) => Promise<TerminalSession>) | undefined,
  reconnect: boolean,
): Promise<TerminalSession> {
  if (open !== undefined) {
    return open(size);
  }
  return reconnect
    ? reconnectTerminal(connectionId, size).then((reconnected) => reconnected.session)
    : openTerminal(connectionId, size);
}

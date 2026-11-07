import { useEffect } from 'react';
import { openWebSocket } from '../api/webSocket';
import type { DiagnosticSnapshot } from '../api/networkDiagnostics';
import { getNetworkDiagnostics, receiveDiagnosticSnapshot, refreshDiagnostic } from '../state/networkDiagnostics';

export function useNetworkDiagnosticFeed() {
  useEffect(() => {
    let stopped = false;
    let socket: WebSocket | null = null;
    let reconnect: ReturnType<typeof setTimeout> | undefined;
    const retry = () => { if (!stopped) reconnect = setTimeout(connect, 2000); };
    const connect = () => {
      if (stopped) return;
      try { socket = openWebSocket('/api/ws'); }
      catch { retry(); return; }
      socket.onopen = () => { void refreshDiagnostic(); };
      socket.onmessage = (event: MessageEvent<string>) => {
        try {
          const message = JSON.parse(event.data) as { type: string; data: DiagnosticSnapshot };
          if (message.type === 'network-diagnostics.changed') receiveDiagnosticSnapshot(message.data);
        } catch { void refreshDiagnostic(); }
      };
      socket.onclose = retry;
    };
    connect();
    void refreshDiagnostic();
    const poll = setInterval(() => {
      const run = getNetworkDiagnostics().run;
      if (run?.status === 'running' || run?.status === 'queued') void refreshDiagnostic();
    }, 3000);
    return () => {
      stopped = true;
      clearTimeout(reconnect);
      clearInterval(poll);
      if (socket !== null) { socket.onclose = null; socket.onmessage = null; socket.onopen = null; socket.close(); }
    };
  }, []);
}

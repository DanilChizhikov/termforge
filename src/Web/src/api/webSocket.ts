export interface LocationLike {
  protocol: string;
  host: string;
}

export type WebSocketQuery = Record<string, string | number | boolean>;

export function buildWebSocketUrl(
  path: string,
  query: WebSocketQuery = {},
  location: LocationLike = window.location,
): string {
  if (!path.startsWith('/api/')) {
    throw new Error(`WebSocket path must start with /api/: ${path}`);
  }
  const scheme = location.protocol === 'https:' ? 'wss:' : 'ws:';
  const search = new URLSearchParams();
  Object.entries(query).forEach(([key, value]) => search.set(key, String(value)));
  const queryString = search.toString();
  return `${scheme}//${location.host}${path}${queryString.length > 0 ? `?${queryString}` : ''}`;
}

export function openWebSocket(path: string, query: WebSocketQuery = {}): WebSocket {
  return new WebSocket(buildWebSocketUrl(path, query));
}

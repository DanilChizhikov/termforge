export interface SessionInfo {
  csrfToken: string;
}

export type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';

export class ApiError extends Error {
  readonly status: number;
  readonly body: unknown;

  constructor(status: number, body: unknown) {
    super(`API request failed with status ${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.body = body;
  }

  get isSessionMissing(): boolean {
    return this.status === 401;
  }
}

export interface ApiClientOptions {
  fetchImpl?: typeof fetch;
  onSessionMissing?: () => void;
}

const SESSION_PATH = '/api/session';
const CSRF_HEADER = 'X-CSRF-Token';
const UNSAFE_METHODS: ReadonlySet<HttpMethod> = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);

export class ApiClient {
  private readonly fetchImpl: typeof fetch;
  private readonly onSessionMissing: (() => void) | undefined;
  private csrfToken: string | null = null;
  private csrfRequest: Promise<string> | null = null;

  constructor(options: ApiClientOptions = {}) {
    this.fetchImpl = options.fetchImpl ?? ((input, init) => globalThis.fetch(input, init));
    this.onSessionMissing = options.onSessionMissing;
  }

  async getSession(): Promise<SessionInfo> {
    const response = await this.fetchImpl(SESSION_PATH, {
      method: 'GET',
      credentials: 'same-origin',
      headers: { Accept: 'application/json' },
    });
    const body = await readBody(response);
    if (!response.ok) {
      this.handleFailure(response.status);
      throw new ApiError(response.status, body);
    }
    const session = body as SessionInfo;
    this.csrfToken = session.csrfToken;
    return session;
  }

  get<T>(path: string): Promise<T> {
    return this.request<T>('GET', path);
  }

  post<T>(path: string, body?: unknown): Promise<T> {
    return this.request<T>('POST', path, body);
  }

  put<T>(path: string, body?: unknown): Promise<T> {
    return this.request<T>('PUT', path, body);
  }

  patch<T>(path: string, body?: unknown): Promise<T> {
    return this.request<T>('PATCH', path, body);
  }

  delete<T>(path: string): Promise<T> {
    return this.request<T>('DELETE', path);
  }

  async openStream(path: string, signal: AbortSignal): Promise<Response> {
    assertApiPath(path);
    const response = await this.fetchImpl(path, {
      method: 'GET',
      credentials: 'same-origin',
      headers: { Accept: 'application/x-ndjson, application/json' },
      signal,
    });
    if (!response.ok) {
      const payload = await readBody(response);
      this.handleFailure(response.status);
      throw new ApiError(response.status, payload);
    }
    return response;
  }

  async request<T>(method: HttpMethod, path: string, body?: unknown): Promise<T> {
    assertApiPath(path);
    const isUnsafe = UNSAFE_METHODS.has(method);
    const tokenWasCached = this.csrfToken !== null;
    let response = await this.send(method, path, body, isUnsafe);

    if (isUnsafe && response.status === 403 && tokenWasCached) {
      this.csrfToken = null;
      response = await this.send(method, path, body, isUnsafe);
    }

    const payload = await readBody(response);
    if (!response.ok) {
      this.handleFailure(response.status);
      throw new ApiError(response.status, payload);
    }
    return payload as T;
  }

  private async send(
    method: HttpMethod,
    path: string,
    body: unknown,
    isUnsafe: boolean,
  ): Promise<Response> {
    const headers: Record<string, string> = { Accept: 'application/json' };
    if (isUnsafe) {
      headers[CSRF_HEADER] = await this.ensureCsrfToken();
    }
    const init: RequestInit = { method, credentials: 'same-origin', headers };
    if (body !== undefined) {
      headers['Content-Type'] = 'application/json';
      init.body = JSON.stringify(body);
    }
    return this.fetchImpl(path, init);
  }

  private ensureCsrfToken(): Promise<string> {
    if (this.csrfToken !== null) {
      return Promise.resolve(this.csrfToken);
    }
    this.csrfRequest ??= this.getSession()
      .then((session) => session.csrfToken)
      .finally(() => {
        this.csrfRequest = null;
      });
    return this.csrfRequest;
  }

  private handleFailure(status: number): void {
    if (status === 401) {
      this.csrfToken = null;
      this.onSessionMissing?.();
    }
  }
}

function assertApiPath(path: string): void {
  if (!path.startsWith('/api/') && path !== '/api') {
    throw new Error(`API path must start with /api: ${path}`);
  }
}

async function readBody(response: Response): Promise<unknown> {
  const text = await response.text();
  if (text.length === 0) {
    return undefined;
  }
  try {
    return JSON.parse(text) as unknown;
  } catch {
    return text;
  }
}

let sessionMissingListeners: Array<() => void> = [];

export function subscribeSessionMissing(listener: () => void): () => void {
  sessionMissingListeners = [...sessionMissingListeners, listener];
  return () => {
    sessionMissingListeners = sessionMissingListeners.filter((item) => item !== listener);
  };
}

export const apiClient = new ApiClient({
  onSessionMissing: () => {
    sessionMissingListeners.forEach((listener) => listener());
  },
});

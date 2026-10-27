import { apiClient, type ApiClient } from './client';

const LOGIN_PATH = '/api/session/login';
const LOGOUT_PATH = '/api/session/logout';

export interface LoginStatus {
  passwordLoginEnabled: boolean;
  signedInWithPassword: boolean;
}

const LOGIN_DISABLED: LoginStatus = { passwordLoginEnabled: false, signedInWithPassword: false };

export type SignInResult =
  | { kind: 'signed-in' }
  | { kind: 'wrong-password' }
  | { kind: 'locked-out'; retryAfterSeconds: number | null }
  | { kind: 'failed'; status: number };

export async function getLoginStatus(): Promise<LoginStatus> {
  try {
    const response = await globalThis.fetch(LOGIN_PATH, {
      method: 'GET',
      credentials: 'same-origin',
      headers: { Accept: 'application/json' },
    });
    if (!response.ok) {
      return LOGIN_DISABLED;
    }
    const status = (await response.json()) as Partial<LoginStatus>;
    return {
      passwordLoginEnabled: status.passwordLoginEnabled === true,
      signedInWithPassword: status.signedInWithPassword === true,
    };
  } catch {
    return LOGIN_DISABLED;
  }
}

export async function signIn(password: string): Promise<SignInResult> {
  const response = await globalThis.fetch(LOGIN_PATH, {
    method: 'POST',
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ password }),
  });
  if (response.ok) {
    return { kind: 'signed-in' };
  }
  if (response.status === 401) {
    return { kind: 'wrong-password' };
  }
  if (response.status === 429) {
    return { kind: 'locked-out', retryAfterSeconds: parseRetryAfter(response.headers.get('Retry-After')) };
  }
  return { kind: 'failed', status: response.status };
}

export function signOut(client: ApiClient = apiClient): Promise<void> {
  return client.post<void>(LOGOUT_PATH);
}

export function isInsecureNetworkConnection(location: Pick<Location, 'protocol' | 'hostname'>): boolean {
  return location.protocol === 'http:' && !isLoopbackHostname(location.hostname);
}

function isLoopbackHostname(hostname: string): boolean {
  const host = hostname.replace(/^\[|\]$/g, '').toLowerCase();
  return host === 'localhost' || host === '::1' || /^127\.\d{1,3}\.\d{1,3}\.\d{1,3}$/.test(host);
}

function parseRetryAfter(value: string | null): number | null {
  if (value === null) {
    return null;
  }
  const seconds = Number.parseInt(value, 10);
  return Number.isFinite(seconds) ? seconds : null;
}

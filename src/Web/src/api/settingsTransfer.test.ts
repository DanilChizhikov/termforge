import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from './client';
import { applyImport, decodeBase64, encodeBase64, exportSettings, type SettingsImportRequest } from './settingsTransfer';

interface RecordedCall {
  url: string;
  method: string;
  body: unknown;
}

function recordingClient(calls: RecordedCall[]): ApiClient {
  const fetchImpl = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
    const url = typeof input === 'string' ? input : input.toString();
    if (url === '/api/session') {
      return Promise.resolve(new Response(JSON.stringify({ csrfToken: 'token' }), { status: 200 }));
    }
    const body = typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined;
    calls.push({ url, method: init?.method ?? 'GET', body });
    return Promise.resolve(new Response(JSON.stringify({}), { status: 200 }));
  });
  return new ApiClient({ fetchImpl: fetchImpl as unknown as typeof fetch });
}

describe('settings transfer api', () => {
  it('round-trips binary archives through base64', () => {
    const bytes = new Uint8Array(70000);
    for (let i = 0; i < bytes.length; i++) {
      bytes[i] = (i * 31) % 256;
    }

    expect(decodeBase64(encodeBase64(bytes))).toEqual(bytes);
  });

  it('posts the export request with the chosen sections and secrets', async () => {
    const calls: RecordedCall[] = [];
    const request = {
      sections: { connections: true, preferences: false, workspace: true, pinnedCommands: false },
      secrets: { password: 'correct-horse-staple', confirmed: true },
    };

    await exportSettings(request, recordingClient(calls));

    expect(calls).toEqual([{ url: '/api/settings-transfer/export', method: 'POST', body: request }]);
  });

  it('posts the import request to the apply endpoint', async () => {
    const calls: RecordedCall[] = [];
    const request: SettingsImportRequest = {
      settingsFile: '{}',
      secretsArchive: null,
      password: null,
      mode: 'replace',
      sections: { connections: true, preferences: true, workspace: true, pinnedCommands: true },
      resolutions: { 'id-1': 'keepBoth' },
      confirmReplace: true,
    };

    await applyImport(request, recordingClient(calls));

    expect(calls[0]?.url).toBe('/api/settings-transfer/import');
    expect(calls[0]?.body).toEqual(request);
  });
});

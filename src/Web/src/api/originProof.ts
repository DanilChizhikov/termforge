const STORAGE_KEY = 'termforge.origin-proof';

export function getOriginProof(): string | null {
  return sessionStorage.getItem(STORAGE_KEY);
}

export function saveOriginProof(token: string): void {
  sessionStorage.setItem(STORAGE_KEY, token);
}

export function clearOriginProof(): void {
  sessionStorage.removeItem(STORAGE_KEY);
}

export function originProofHeaders(): Record<string, string> {
  const token = getOriginProof();
  return token === null ? {} : { 'X-CSRF-Token': token };
}

export function originProofProtocols(): string[] {
  const token = getOriginProof();
  return token === null ? [] : [`termforge.${token}`];
}

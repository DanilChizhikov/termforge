export function readThemeToken(name: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}

export function readThemeTokenHex(name: string): string {
  return readThemeToken(name).replace(/^#/, '');
}

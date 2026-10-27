import type { ITheme } from '@xterm/xterm';
import { readThemeToken } from './themeTokens';

export const TERMINAL_FONT_FAMILY = "'Geist Mono Variable', ui-monospace, 'SF Mono', Menlo, Consolas, monospace";

export const TERMINAL_FONT_SIZE = 12.5;

export const TERMINAL_LINE_HEIGHT = 1.6;

export function createTerminalTheme(): ITheme {
  return {
    background: readThemeToken('--surface-terminal'),
    foreground: readThemeToken('--terminal-foreground'),
    cursor: readThemeToken('--accent'),
    cursorAccent: readThemeToken('--surface-terminal'),
    selectionBackground: readThemeToken('--terminal-selection'),
    black: readThemeToken('--surface-card'),
    red: readThemeToken('--status-danger'),
    green: readThemeToken('--status-ok'),
    yellow: readThemeToken('--status-caution'),
    blue: readThemeToken('--status-info'),
    magenta: readThemeToken('--terminal-magenta'),
    cyan: readThemeToken('--terminal-cyan'),
    white: readThemeToken('--terminal-foreground'),
    brightBlack: readThemeToken('--text-muted'),
    brightRed: readThemeToken('--status-danger-text'),
    brightGreen: readThemeToken('--status-ok-text'),
    brightYellow: readThemeToken('--terminal-bright-yellow'),
    brightBlue: readThemeToken('--status-info-text'),
    brightMagenta: readThemeToken('--terminal-bright-magenta'),
    brightCyan: readThemeToken('--terminal-bright-cyan'),
    brightWhite: readThemeToken('--text-primary'),
  };
}

export function loadTerminalFont(): Promise<boolean> {
  const fonts = document.fonts as FontFaceSet | undefined;
  if (fonts === undefined) {
    return Promise.resolve(false);
  }
  return fonts
    .load(`${TERMINAL_FONT_SIZE}px ${TERMINAL_FONT_FAMILY}`)
    .then((loaded) => loaded.length > 0)
    .catch(() => false);
}

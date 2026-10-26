import type { ITheme } from '@xterm/xterm';

export const TERMINAL_FONT_FAMILY = "'Geist Mono Variable', ui-monospace, 'SF Mono', Menlo, Consolas, monospace";

export const TERMINAL_FONT_SIZE = 12.5;

export const TERMINAL_LINE_HEIGHT = 1.35;

export const TERMINAL_THEME: ITheme = {
  background: '#08090b',
  foreground: '#d5d8dd',
  cursor: '#ff8a3d',
  cursorAccent: '#08090b',
  selectionBackground: 'rgba(255, 138, 61, 0.28)',
  black: '#121417',
  red: '#ff6b5e',
  green: '#46c08a',
  yellow: '#e9b949',
  blue: '#6fa8ff',
  magenta: '#c49cff',
  cyan: '#5cc8b0',
  white: '#d5d8dd',
  brightBlack: '#858b94',
  brightRed: '#ff8a80',
  brightGreen: '#7fd8ae',
  brightYellow: '#f2c14e',
  brightBlue: '#9cc3ff',
  brightMagenta: '#dcbcff',
  brightCyan: '#8fe0cc',
  brightWhite: '#eceae6',
};

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

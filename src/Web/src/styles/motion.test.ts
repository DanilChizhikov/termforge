import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

const STYLE_FILES = ['tokens.css', 'base.css', 'components.css', 'layout.css', 'docker.css', 'settings.css', 'recording.css', 'exit.css'];

const STYLES_DIRECTORY = dirname(fileURLToPath(import.meta.url));

const HOVER_QUERY = '@media (hover: hover) and (pointer: fine)';

const MOTION_QUERY = '@media (prefers-reduced-motion: no-preference)';

function readStyle(fileName: string): string {
  const filePath = join(STYLES_DIRECTORY, fileName);
  return readFileSync(filePath, 'utf8');
}

function findBlockEnd(css: string, openBraceIndex: number): number {
  let depth = 0;
  for (let i = openBraceIndex; i < css.length; i++) {
    if (css[i] === '{') {
      depth++;
    } else if (css[i] === '}') {
      depth--;
      if (depth === 0) {
        return i;
      }
    }
  }
  return css.length - 1;
}

function splitByAtRule(css: string, query: string): { inside: string; outside: string } {
  let inside = '';
  let outside = '';
  let cursor = 0;
  let queryIndex = css.indexOf(query, cursor);
  while (queryIndex !== -1) {
    const openBraceIndex = css.indexOf('{', queryIndex);
    const blockEnd = findBlockEnd(css, openBraceIndex);
    outside += css.slice(cursor, queryIndex);
    inside += css.slice(openBraceIndex + 1, blockEnd);
    cursor = blockEnd + 1;
    queryIndex = css.indexOf(query, cursor);
  }
  outside += css.slice(cursor);
  return { inside, outside };
}

function readAllStyles(): string {
  let combined = '';
  for (let i = 0; i < STYLE_FILES.length; i++) {
    const fileName = STYLE_FILES[i] ?? '';
    combined += readStyle(fileName);
  }
  return combined;
}

function selectorsWithColorTransition(css: string): string[] {
  const rulePattern = /([^{}]+)\{[^{}]*transition:[^;]*color[^;]*;[^{}]*\}/g;
  const selectors: string[] = [];
  for (const match of css.matchAll(rulePattern)) {
    const selector = match[1] ?? '';
    selectors.push(selector.trim());
  }
  return selectors;
}

describe('motion styles', () => {
  it('defines motion tokens', () => {
    const tokens = readStyle('tokens.css');

    expect(tokens).toContain('--ease-out: cubic-bezier(0.23, 1, 0.32, 1)');
    expect(tokens).toContain('--duration-press:');
    expect(tokens).toContain('--duration-modal:');
  });

  it('never transitions all properties or uses ease-in', () => {
    const styles = readAllStyles();

    expect(styles).not.toMatch(/transition:\s*all/);
    expect(styles).not.toMatch(/ease-in(?!-out)/);
  });

  it('takes transition durations only from tokens', () => {
    const styles = readAllStyles();

    expect(styles).not.toMatch(/transition:[^;]*\b\d+(\.\d+)?m?s\b/);
  });

  it('keeps hover color transitions on devices with a precise pointer', () => {
    const styles = readAllStyles();
    const { outside } = splitByAtRule(styles, HOVER_QUERY);

    expect(selectorsWithColorTransition(outside)).toEqual(['.switch']);
  });

  it('applies transform motion only without a reduced motion preference', () => {
    const styles = readAllStyles();
    const { inside, outside } = splitByAtRule(styles, MOTION_QUERY);

    expect(inside).toContain('scale(0.97)');
    expect(inside).toContain('scale(0.96)');
    expect(outside).not.toMatch(/scale\(/);
  });
});

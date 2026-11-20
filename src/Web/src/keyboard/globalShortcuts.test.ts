import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { installGlobalShortcuts } from './globalShortcuts';

const shortcut = { code: 'KeyX', ctrlKey: true, shiftKey: true };
let dispose: (() => void) | undefined;
let testArea: HTMLDivElement;

function createKeyDown(init: KeyboardEventInit): KeyboardEvent {
  return new KeyboardEvent('keydown', { bubbles: true, cancelable: true, ...init });
}

beforeEach(() => {
  vi.spyOn(navigator, 'platform', 'get').mockReturnValue('Win32');
  vi.spyOn(navigator, 'userAgent', 'get').mockReturnValue('Chrome/135.0 Safari/537.36');
  testArea = document.createElement('div');
  document.body.append(testArea);
});

afterEach(() => {
  dispose?.();
  dispose = undefined;
  vi.restoreAllMocks();
  testArea.remove();
});

describe('global shortcuts', () => {
  it.each([
    ['Win32', { ctrlKey: true }, { metaKey: true }],
    ['Linux x86_64', { ctrlKey: true }, { metaKey: true }],
    ['MacIntel', { metaKey: true }, { ctrlKey: true }],
  ])('uses the platform modifier on %s', (platform, mod, otherMod) => {
    vi.spyOn(navigator, 'platform', 'get').mockReturnValue(platform);
    const run = vi.fn();
    dispose = installGlobalShortcuts([{ code: 'KeyX', shift: true, run }]);

    const recognized = createKeyDown({ code: 'KeyX', key: 'ч', shiftKey: true, ...mod });
    const wrongModifier = createKeyDown({ code: 'KeyX', shiftKey: true, ...otherMod });
    window.dispatchEvent(recognized);
    window.dispatchEvent(wrongModifier);

    expect(recognized.defaultPrevented).toBe(true);
    expect(wrongModifier.defaultPrevented).toBe(false);
    expect(run).toHaveBeenCalledTimes(1);
  });

  it.each(['xterm', 'monaco-editor'])('captures a shortcut from a focused %s descendant', (widgetClass) => {
    const widget = document.createElement('div');
    widget.className = widgetClass;
    const textarea = document.createElement('textarea');
    widget.append(textarea);
    testArea.append(widget);
    textarea.focus();

    const localKeyDown = vi.fn();
    textarea.addEventListener('keydown', localKeyDown, true);
    const recognized = createKeyDown(shortcut);
    const run = vi.fn(() => expect(recognized.defaultPrevented).toBe(true));
    dispose = installGlobalShortcuts([{ code: 'KeyX', shift: true, run }]);

    expect(textarea).toHaveFocus();
    textarea.dispatchEvent(recognized);
    expect(run).toHaveBeenCalledOnce();
    expect(localKeyDown).not.toHaveBeenCalled();

    const ordinaryKey = createKeyDown({ code: 'KeyC', key: 'c' });
    textarea.dispatchEvent(ordinaryKey);
    expect(ordinaryKey.defaultPrevented).toBe(false);
    expect(localKeyDown).toHaveBeenCalledExactlyOnceWith(ordinaryKey);
  });

  it.each([
    ['missing Shift', { shiftKey: false }],
    ['missing Mod', { ctrlKey: false }],
    ['both Mod keys', { metaKey: true }],
    ['Alt', { altKey: true }],
    ['AltGraph', { modifierAltGraph: true }],
    ['IME composition', { isComposing: true }],
    ['uncancelable event', { cancelable: false }],
    ['unregistered code', { code: 'KeyY' }],
  ] as const)('passes through %s', (_case, overrides) => {
    const run = vi.fn();
    dispose = installGlobalShortcuts([{ code: 'KeyX', shift: true, run }]);
    const event = createKeyDown({ ...shortcut, ...overrides });
    const target = document.createElement('textarea');
    testArea.append(target);
    const localKeyDown = vi.fn();
    target.addEventListener('keydown', localKeyDown, true);

    target.dispatchEvent(event);

    expect(event.defaultPrevented).toBe(false);
    expect(localKeyDown).toHaveBeenCalledExactlyOnceWith(event);
    expect(run).not.toHaveBeenCalled();
  });

  it('passes through a previously cancelled event', () => {
    const run = vi.fn();
    dispose = installGlobalShortcuts([{ code: 'KeyX', shift: true, run }]);
    const event = createKeyDown(shortcut);
    event.preventDefault();
    const target = document.createElement('textarea');
    testArea.append(target);
    const localKeyDown = vi.fn();
    target.addEventListener('keydown', localKeyDown, true);

    target.dispatchEvent(event);

    expect(localKeyDown).toHaveBeenCalledExactlyOnceWith(event);
    expect(run).not.toHaveBeenCalled();
  });

  it.each([
    ['KeyW', true], ['KeyT', true], ['KeyN', true], ['KeyR', true],
    ['KeyW', false], ['KeyT', false], ['KeyN', false], ['KeyL', false], ['KeyR', false],
    ['KeyK', false], ['KeyS', false], ['Digit1', false], ['Digit9', false],
  ] as const)('passes through registered browser shortcut %s (Shift: %s)', (code, shift) => {
    const run = vi.fn();
    dispose = installGlobalShortcuts([{ code, shift, run }]);
    const event = createKeyDown({ code, ctrlKey: true, shiftKey: shift });

    window.dispatchEvent(event);

    expect(event.defaultPrevented).toBe(false);
    expect(run).not.toHaveBeenCalled();
  });

  it.each([
    ['MacIntel', 'Chrome/135.0 Safari/537.36', 'Digit4'],
    ['Win32', 'Chrome/135.0 Safari/537.36', 'Escape'],
    ['Win32', 'Chrome/135.0 Safari/537.36 Edg/135.0', 'KeyE'],
    ['Win32', 'Chrome/135.0 Safari/537.36 Edg/135.0', 'KeyK'],
    ['Win32', 'Chrome/135.0 Safari/537.36 Edg/135.0', 'KeyL'],
    ['MacIntel', 'Chrome/135.0 Safari/537.36 Edg/135.0', 'KeyE'],
    ['Linux x86_64', 'Firefox/136.0', 'KeyK'],
    ['Linux x86_64', 'Firefox/136.0', 'KeyE'],
    ['MacIntel', 'Version/26.0 Safari/605.1.15', 'KeyL'],
  ])('passes through reserved shortcut on %s with %s (%s)', (platform, userAgent, code) => {
    vi.spyOn(navigator, 'platform', 'get').mockReturnValue(platform);
    vi.spyOn(navigator, 'userAgent', 'get').mockReturnValue(userAgent);
    const run = vi.fn();
    dispose = installGlobalShortcuts([{ code, shift: true, run }]);
    const isMac = platform.startsWith('Mac');
    const event = createKeyDown({ code, shiftKey: true, ctrlKey: !isMac, metaKey: isMac });

    window.dispatchEvent(event);

    expect(event.defaultPrevented).toBe(false);
    expect(run).not.toHaveBeenCalled();
  });

  it.each(['KeyE', 'KeyK', 'KeyL', 'Digit1', 'Digit9'])('lets Chrome register Shift+%s', (code) => {
    const run = vi.fn();
    dispose = installGlobalShortcuts([{ code, shift: true, run }]);
    const event = createKeyDown({ code, ctrlKey: true, shiftKey: true });

    window.dispatchEvent(event);

    expect(event.defaultPrevented).toBe(true);
    expect(run).toHaveBeenCalledOnce();
  });

  it('recognizes a binding without Shift only when Shift is released', () => {
    const run = vi.fn();
    dispose = installGlobalShortcuts([{ code: 'F8', shift: false, run }]);
    const plain = createKeyDown({ code: 'F8', ctrlKey: true });
    const shifted = createKeyDown({ code: 'F8', ctrlKey: true, shiftKey: true });

    window.dispatchEvent(plain);
    window.dispatchEvent(shifted);

    expect(plain.defaultPrevented).toBe(true);
    expect(shifted.defaultPrevented).toBe(false);
    expect(run).toHaveBeenCalledOnce();
  });

  it('cancels repeats without dispatching them to the command or target', () => {
    const run = vi.fn();
    dispose = installGlobalShortcuts([{ code: 'KeyX', shift: true, run }]);
    const target = document.createElement('textarea');
    testArea.append(target);
    const localKeyDown = vi.fn();
    target.addEventListener('keydown', localKeyDown, true);
    const first = createKeyDown(shortcut);
    const repeat = createKeyDown({ ...shortcut, repeat: true });

    target.dispatchEvent(first);
    target.dispatchEvent(repeat);

    expect(run).toHaveBeenCalledOnce();
    expect(repeat.defaultPrevented).toBe(true);
    expect(localKeyDown).not.toHaveBeenCalled();
  });

  it('stops intercepting events after disposal and allows reinstallation', () => {
    const firstRun = vi.fn();
    const uninstall = installGlobalShortcuts([{ code: 'KeyX', shift: true, run: firstRun }]);
    uninstall();
    const afterDisposal = createKeyDown(shortcut);
    window.dispatchEvent(afterDisposal);

    const nextRun = vi.fn();
    dispose = installGlobalShortcuts([{ code: 'KeyX', shift: true, run: nextRun }]);
    const afterReinstall = createKeyDown(shortcut);
    window.dispatchEvent(afterReinstall);

    expect(afterDisposal.defaultPrevented).toBe(false);
    expect(afterReinstall.defaultPrevented).toBe(true);
    expect(firstRun).not.toHaveBeenCalled();
    expect(nextRun).toHaveBeenCalledOnce();
  });
});

export interface GlobalShortcutBinding {
  code: string;
  shift: boolean;
  run: () => void;
}

const RESERVED_CODES = new Set([
  'KeyD', 'KeyF', 'KeyG', 'KeyK', 'KeyL', 'KeyN', 'KeyO', 'KeyP', 'KeyR', 'KeyS', 'KeyT', 'KeyW',
  'Digit0', 'Digit1', 'Digit2', 'Digit3', 'Digit4', 'Digit5', 'Digit6', 'Digit7', 'Digit8', 'Digit9',
  'Equal', 'Minus', 'NumpadAdd', 'NumpadSubtract', 'Tab',
]);

const RESERVED_SHIFT_CODES = new Set([
  'KeyB', 'KeyD', 'KeyG', 'KeyN', 'KeyP', 'KeyR', 'KeyT', 'KeyV', 'KeyW', 'Delete', 'Equal', 'Tab',
]);

const MAC_RESERVED_CODES = new Set([
  'ArrowLeft', 'ArrowRight', 'Backquote', 'BracketLeft', 'BracketRight', 'Comma', 'KeyH', 'KeyM', 'KeyQ',
  'KeyY', 'Space',
]);

const MAC_RESERVED_SHIFT_CODES = new Set([
  'Backquote', 'BracketLeft', 'BracketRight', 'Digit3', 'Digit4', 'Digit5', 'Digit6', 'KeyH',
  'KeyJ', 'KeyQ', 'KeyZ', 'Space',
]);

const CONTROL_RESERVED_CODES = new Set([
  'Escape', 'F4', 'F5', 'F6', 'KeyE', 'KeyH', 'KeyJ', 'KeyQ', 'KeyU', 'PageDown', 'PageUp',
]);

const CONTROL_RESERVED_SHIFT_CODES = new Set([
  'Escape', 'KeyC', 'KeyI', 'KeyJ', 'KeyO', 'KeyQ', 'PageDown', 'PageUp',
]);

function getBrowserReservedShiftCodes(isMac: boolean, userAgent: string): readonly string[] {
  if (userAgent.includes('Edg/')) {
    return isMac ? ['KeyC', 'KeyE', 'KeyM', 'KeyU'] : ['KeyE', 'KeyK', 'KeyL', 'KeyM', 'KeyU', 'KeyY'];
  }
  if (userAgent.includes('Firefox/')) {
    return isMac ? ['KeyA', 'KeyS'] : ['KeyA', 'KeyE', 'KeyK', 'KeyS', 'KeyY'];
  }
  if (isMac && userAgent.includes('Version/') && userAgent.includes('Safari/')) {
    return ['Backslash', 'KeyL'];
  }
  return ['KeyM'];
}

export function installGlobalShortcuts(bindings: readonly GlobalShortcutBinding[]): () => void {
  const isMac = navigator.platform.startsWith('Mac');
  const platformReservedCodes = isMac ? MAC_RESERVED_CODES : CONTROL_RESERVED_CODES;
  const platformReservedShiftCodes = isMac ? MAC_RESERVED_SHIFT_CODES : CONTROL_RESERVED_SHIFT_CODES;
  const browserReservedShiftCodes = getBrowserReservedShiftCodes(isMac, navigator.userAgent);

  function handleKeyDown(event: KeyboardEvent): void {
    const { code, shiftKey, metaKey, ctrlKey } = event;
    if (event.defaultPrevented || !event.cancelable || event.isComposing || event.altKey
      || event.getModifierState('AltGraph') || metaKey !== isMac || ctrlKey === isMac) {
      return;
    }

    const reserved = shiftKey
      ? RESERVED_SHIFT_CODES.has(code) || platformReservedShiftCodes.has(code) || browserReservedShiftCodes.includes(code)
      : RESERVED_CODES.has(code) || platformReservedCodes.has(code);
    if (reserved) {
      return;
    }

    const binding = bindings.find((candidate) => candidate.code === code && candidate.shift === shiftKey);
    if (binding === undefined) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    if (!event.repeat) {
      binding.run();
    }
  }

  window.addEventListener('keydown', handleKeyDown, true);
  return () => window.removeEventListener('keydown', handleKeyDown, true);
}

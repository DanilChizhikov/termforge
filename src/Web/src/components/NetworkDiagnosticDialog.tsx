import { useEffect, useId, useRef, type KeyboardEvent, type ReactNode } from 'react';
import { createPortal } from 'react-dom';

interface Props {
  title: string;
  onClose: () => void;
  children: ReactNode;
}

export function NetworkDiagnosticDialog({ title, onClose, children }: Props) {
  const titleId = useId();
  const card = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const previous = document.activeElement;
    card.current?.querySelector<HTMLElement>('input:not(:disabled), button:not(:disabled), select:not(:disabled)')?.focus();
    return () => { if (previous instanceof HTMLElement && previous.isConnected) previous.focus({ preventScroll: true }); };
  }, []);

  function handleKey(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      onClose();
      return;
    }
    if (event.key !== 'Tab') return;
    const controls = card.current?.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), select:not(:disabled), [tabindex="0"]');
    if (controls === undefined || controls.length === 0) return;
    const first = controls[0];
    const last = controls[controls.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last?.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first?.focus();
    }
  }

  return createPortal(
    <div className="dialog-backdrop">
      <div ref={card} role="dialog" aria-modal="true" aria-labelledby={titleId} className="dialog-card network-dialog" onKeyDown={handleKey}>
        <div className="dialog-header"><h2 id={titleId} className="dialog-title">{title}</h2></div>
        {children}
      </div>
    </div>,
    document.body,
  );
}

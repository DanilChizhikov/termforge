import { useEffect, useId, useRef } from 'react';
import { createPortal } from 'react-dom';

interface TerminalCloseDialogProps {
  title: string;
  onCancel: () => void;
  onConfirm: () => void;
}

export function TerminalCloseDialog({ title, onCancel, onConfirm }: TerminalCloseDialogProps) {
  const dialog = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  const descriptionId = useId();

  useEffect(() => {
    const element = dialog.current;
    element?.showModal();
    return () => element?.close();
  }, []);

  return createPortal(
    <dialog
      ref={dialog}
      className="dialog-card terminal-close-dialog"
      role="alertdialog"
      aria-labelledby={titleId}
      aria-describedby={descriptionId}
      onCancel={(event) => { event.preventDefault(); onCancel(); }}
    >
      <div className="dialog-header">
        <h2 className="dialog-title" id={titleId}>Close terminal session?</h2>
        <p className="text-secondary" id={descriptionId}>
          Closing <span className="mono">{title}</span> ends its active shell session.
        </p>
      </div>
      <div className="dialog-footer">
        <button type="button" className="btn btn-safe" autoFocus onClick={onCancel}>Cancel</button>
        <button type="button" className="btn btn-destructive" onClick={onConfirm}>Close session</button>
      </div>
    </dialog>,
    document.body,
  );
}

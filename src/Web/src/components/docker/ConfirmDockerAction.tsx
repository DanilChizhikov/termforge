import { useId, useState, type KeyboardEvent } from 'react';

export interface ConfirmDockerActionProps {
  title: string;
  consequence: string;
  confirmLabel: string;
  offerSkip?: boolean;
  onConfirm: (skipNextTime: boolean) => void;
  onCancel: () => void;
}

export function ConfirmDockerAction({
  title,
  consequence,
  confirmLabel,
  offerSkip = false,
  onConfirm,
  onCancel,
}: ConfirmDockerActionProps) {
  const titleId = useId();
  const [skipNextTime, setSkipNextTime] = useState(false);

  const closeOnEscape = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape') {
      event.stopPropagation();
      onCancel();
    }
  };

  return (
    <div role="dialog" aria-modal="false" aria-labelledby={titleId} className="docker-confirm" onKeyDown={closeOnEscape}>
      <p id={titleId} className="docker-confirm-title">
        {title}
      </p>
      <p className="docker-confirm-text">{consequence} The action is recorded in the journal.</p>
      {offerSkip ? (
        <label className="check-row docker-confirm-skip">
          <input
            type="checkbox"
            className="checkbox"
            checked={skipNextTime}
            onChange={(event) => setSkipNextTime(event.target.checked)}
          />
          Don&apos;t ask again
        </label>
      ) : null}
      <div className="docker-confirm-actions">
        <button type="button" className="btn btn-sm btn-safe" autoFocus onClick={onCancel}>
          Cancel
        </button>
        <button type="button" className="btn btn-sm btn-destructive" onClick={() => onConfirm(skipNextTime)}>
          {confirmLabel}
        </button>
      </div>
    </div>
  );
}

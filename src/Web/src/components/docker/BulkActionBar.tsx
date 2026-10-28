import type { ReactNode } from 'react';

export interface BulkActionButton<Action extends string> {
  action: Action;
  label: string;
  ariaLabel: string;
  confirms: boolean;
}

export interface BulkActionBarProps<Action extends string> {
  summary: string;
  buttons: BulkActionButton<Action>[];
  armedAction: Action | null;
  disabled: boolean;
  confirmation: ReactNode;
  onAction: (action: Action) => void;
  onClear: () => void;
}

export function BulkActionBar<Action extends string>({
  summary,
  buttons,
  armedAction,
  disabled,
  confirmation,
  onAction,
  onClear,
}: BulkActionBarProps<Action>) {
  return (
    <section className="docker-bulk" aria-label="Selection">
      <div className="docker-bulk-bar">
        <span className="docker-bulk-summary">{summary}</span>
        <span className="docker-header-spacer" />
        {buttons.map((button) => {
          const armed = armedAction === button.action;
          return (
            <button
              key={button.action}
              type="button"
              className={armed ? 'btn btn-sm docker-row-button docker-row-button-armed' : 'btn btn-sm docker-row-button'}
              disabled={disabled}
              aria-label={button.ariaLabel}
              aria-expanded={button.confirms ? armed : undefined}
              onClick={() => onAction(button.action)}
            >
              {button.label}
            </button>
          );
        })}
        <button type="button" className="btn btn-sm docker-row-button" onClick={onClear}>
          Clear selection
        </button>
      </div>
      {confirmation}
    </section>
  );
}

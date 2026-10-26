import { useState, type FormEvent } from 'react';
import { EMPTY_FILTERS, OPERATION_STATUSES, type OperationFilters, type OperationStatus } from '../api/operations';

interface OperationFilterFormProps {
  onApply: (filters: OperationFilters) => void;
}

export function OperationFilterForm({ onApply }: OperationFilterFormProps) {
  const [draft, setDraft] = useState<OperationFilters>(EMPTY_FILTERS);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    onApply(draft);
  };

  const reset = () => {
    setDraft(EMPTY_FILTERS);
    onApply(EMPTY_FILTERS);
  };

  return (
    <form className="operation-filters" aria-label="Operation filters" onSubmit={submit}>
      <label className="field">
        <span className="field-label">Kind</span>
        <input
          className="input input-sm"
          value={draft.kind}
          onChange={(event) => setDraft({ ...draft, kind: event.target.value })}
        />
      </label>
      <label className="field">
        <span className="field-label">Status</span>
        <select
          className="input input-sm"
          value={draft.status}
          onChange={(event) => setDraft({ ...draft, status: event.target.value as OperationStatus | '' })}
        >
          <option value="">Any</option>
          {OPERATION_STATUSES.map((status) => (
            <option key={status} value={status}>
              {status}
            </option>
          ))}
        </select>
      </label>
      <label className="field">
        <span className="field-label">Server</span>
        <input
          className="input input-sm"
          value={draft.server}
          onChange={(event) => setDraft({ ...draft, server: event.target.value })}
        />
      </label>
      <label className="field">
        <span className="field-label">From</span>
        <input
          className="input input-sm"
          type="datetime-local"
          value={draft.from}
          onChange={(event) => setDraft({ ...draft, from: event.target.value })}
        />
      </label>
      <label className="field">
        <span className="field-label">To</span>
        <input
          className="input input-sm"
          type="datetime-local"
          value={draft.to}
          onChange={(event) => setDraft({ ...draft, to: event.target.value })}
        />
      </label>
      <div className="operation-filter-actions">
        <button type="button" className="btn btn-sm" onClick={reset}>
          Reset
        </button>
        <button type="submit" className="btn btn-sm btn-filled">
          Apply
        </button>
      </div>
    </form>
  );
}

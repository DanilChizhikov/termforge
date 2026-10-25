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
      <label>
        Kind
        <input value={draft.kind} onChange={(event) => setDraft({ ...draft, kind: event.target.value })} />
      </label>
      <label>
        Status
        <select
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
      <label>
        Server
        <input value={draft.server} onChange={(event) => setDraft({ ...draft, server: event.target.value })} />
      </label>
      <label>
        From
        <input
          type="datetime-local"
          value={draft.from}
          onChange={(event) => setDraft({ ...draft, from: event.target.value })}
        />
      </label>
      <label>
        To
        <input
          type="datetime-local"
          value={draft.to}
          onChange={(event) => setDraft({ ...draft, to: event.target.value })}
        />
      </label>
      <button type="submit">Apply</button>
      <button type="button" onClick={reset}>
        Reset
      </button>
    </form>
  );
}

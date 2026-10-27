import type { ChangeEvent } from 'react';
import type { OperationFilters, OperationStatus } from '../api/operations';
import { useConnectionsSnapshot } from '../state/connections';

export type OperationPeriod = 'today' | 'week' | 'month' | 'all';

export interface OperationFilterBarProps {
  filters: OperationFilters;
  period: OperationPeriod;
  onChange: (filters: OperationFilters, period: OperationPeriod) => void;
}

const TYPE_OPTIONS: ReadonlyArray<{ kind: string; label: string }> = [
  { kind: '', label: 'All' },
  { kind: 'transfer', label: 'Transfers' },
  { kind: 'file', label: 'File saves' },
  { kind: 'docker', label: 'Docker' },
];

const STATUS_OPTIONS: ReadonlyArray<{ status: OperationStatus | ''; label: string }> = [
  { status: '', label: 'Any' },
  { status: 'failed', label: 'Failed' },
  { status: 'running', label: 'Running' },
  { status: 'queued', label: 'Queued' },
  { status: 'succeeded', label: 'Done' },
  { status: 'cancelled', label: 'Cancelled' },
];

const PERIOD_OPTIONS: ReadonlyArray<{ period: OperationPeriod; label: string }> = [
  { period: 'today', label: 'Today' },
  { period: 'week', label: '7 days' },
  { period: 'month', label: '30 days' },
  { period: 'all', label: 'All time' },
];

const PERIOD_DAYS: Record<Exclude<OperationPeriod, 'all'>, number> = {
  today: 0,
  week: 6,
  month: 29,
};

export function periodStart(period: OperationPeriod, now: Date = new Date()): string {
  if (period === 'all') {
    return '';
  }
  const start = new Date(now.getFullYear(), now.getMonth(), now.getDate() - PERIOD_DAYS[period]);
  return start.toISOString();
}

export function OperationFilterBar({ filters, period, onChange }: OperationFilterBarProps) {
  const connections = useConnectionsSnapshot();
  const serverNames = [...new Set(connections.map((connection) => connection.name))].sort((left, right) =>
    left.localeCompare(right),
  );

  const changeStatus = (event: ChangeEvent<HTMLSelectElement>) => {
    onChange({ ...filters, status: event.target.value as OperationStatus | '' }, period);
  };

  const changePeriod = (event: ChangeEvent<HTMLSelectElement>) => {
    const next = event.target.value as OperationPeriod;
    onChange({ ...filters, from: periodStart(next) }, next);
  };

  return (
    <div className="operation-filters" role="group" aria-label="Operation filters">
      <div role="group" aria-label="Type" className="segmented">
        {TYPE_OPTIONS.map((option) => (
          <button
            key={option.label}
            type="button"
            aria-pressed={filters.kind === option.kind}
            onClick={() => onChange({ ...filters, kind: option.kind }, period)}
          >
            {option.label}
          </button>
        ))}
      </div>
      <label className="operation-filter">
        Status
        <select className="input input-sm" value={filters.status} onChange={changeStatus}>
          {STATUS_OPTIONS.map((option) => (
            <option key={option.label} value={option.status}>
              {option.label}
            </option>
          ))}
        </select>
      </label>
      <label className="operation-filter">
        Server
        <select
          className="input input-sm"
          value={filters.server}
          onChange={(event) => onChange({ ...filters, server: event.target.value }, period)}
        >
          <option value="">All servers</option>
          {serverNames.map((name) => (
            <option key={name} value={name}>
              {name}
            </option>
          ))}
        </select>
      </label>
      <label className="operation-filter">
        Period
        <select className="input input-sm" value={period} onChange={changePeriod}>
          {PERIOD_OPTIONS.map((option) => (
            <option key={option.period} value={option.period}>
              {option.label}
            </option>
          ))}
        </select>
      </label>
    </div>
  );
}

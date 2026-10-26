import { useCallback, useEffect, useRef, useState } from 'react';
import {
  EMPTY_FILTERS,
  PAGE_SIZE,
  listOperations,
  matchesFilters,
  retryOperation,
  type OperationFilters,
  type OperationPage,
  type OperationRecord,
} from '../api/operations';
import { ApiError } from '../api/client';
import { openWebSocket } from '../api/webSocket';
import { EmptyState } from './EmptyState';
import { OperationFilterForm } from './OperationFilterForm';
import { OperationTable } from './OperationTable';

const OPERATION_CHANGED = 'operation.changed';

interface RealtimeEnvelope {
  type: string;
  data: OperationRecord;
}

export function mergeOperation(page: OperationPage, operation: OperationRecord, filters: OperationFilters): OperationPage {
  const existingIndex = page.items.findIndex((item) => item.id === operation.id);
  if (existingIndex >= 0) {
    const items = page.items.map((item) => (item.id === operation.id ? operation : item));
    return { ...page, items };
  }
  if (page.offset !== 0 || !matchesFilters(operation, filters)) {
    return page;
  }
  return { ...page, items: [operation, ...page.items].slice(0, page.limit), total: page.total + 1 };
}

function describeError(error: unknown): string {
  if (error instanceof ApiError) {
    const body = error.body as { detail?: string } | undefined;
    return body?.detail ?? `Request failed with status ${error.status}.`;
  }
  return 'Request failed.';
}

export function OperationHistory() {
  const [filters, setFilters] = useState<OperationFilters>(EMPTY_FILTERS);
  const [offset, setOffset] = useState(0);
  const [page, setPage] = useState<OperationPage | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [live, setLive] = useState(false);
  const filtersRef = useRef(filters);

  useEffect(() => {
    filtersRef.current = filters;
  }, [filters]);

  useEffect(() => {
    let cancelled = false;
    listOperations(filters, offset)
      .then((result) => {
        if (!cancelled) {
          setPage(result);
          setError(null);
        }
      })
      .catch((reason: unknown) => {
        if (!cancelled) setError(describeError(reason));
      });
    return () => {
      cancelled = true;
    };
  }, [filters, offset]);

  useEffect(() => {
    let socket: WebSocket;
    try {
      socket = openWebSocket('/api/ws');
    } catch {
      return undefined;
    }
    socket.onopen = () => setLive(true);
    socket.onclose = () => setLive(false);
    socket.onmessage = (event: MessageEvent<string>) => {
      const envelope = JSON.parse(event.data) as RealtimeEnvelope;
      if (envelope.type !== OPERATION_CHANGED) return;
      setPage((current) => (current === null ? current : mergeOperation(current, envelope.data, filtersRef.current)));
    };
    return () => {
      socket.onclose = null;
      socket.close();
    };
  }, []);

  const applyFilters = useCallback((next: OperationFilters) => {
    setOffset(0);
    setFilters(next);
  }, []);

  const retry = useCallback((operation: OperationRecord) => {
    retryOperation(operation.id)
      .then(() => setError(null))
      .catch((reason: unknown) => setError(describeError(reason)));
  }, []);

  const total = page?.total ?? 0;
  const hasPrevious = offset > 0;
  const hasNext = offset + PAGE_SIZE < total;

  return (
    <div className="view">
      <div className="view-header">
        <div className="view-header-text">
          <h2 className="page-title">Operation history</h2>
        </div>
        <span className={live ? 'status-text status-text-ok' : 'status-text status-text-idle'}>
          <span className={live ? 'status-dot status-dot-ok' : 'status-dot status-dot-idle'} aria-hidden="true" />
          {live ? 'Live updates connected.' : 'Live updates disconnected.'}
        </span>
      </div>
      <OperationFilterForm onApply={applyFilters} />
      {error === null ? null : (
        <p role="alert" className="notice notice-danger">
          {error}
        </p>
      )}
      {page === null || page.items.length === 0 ? (
        <EmptyState message="No operations match." />
      ) : (
        <div className="table-card">
          <OperationTable operations={page.items} onRetry={retry} />
        </div>
      )}
      <div className="operation-paging">
        <button
          type="button"
          className="btn btn-sm"
          disabled={!hasPrevious}
          onClick={() => setOffset(Math.max(0, offset - PAGE_SIZE))}
        >
          Previous
        </button>
        <span>{total} total</span>
        <button type="button" className="btn btn-sm" disabled={!hasNext} onClick={() => setOffset(offset + PAGE_SIZE)}>
          Next
        </button>
      </div>
    </div>
  );
}

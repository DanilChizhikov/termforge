import { useEffect, useRef, useState, type FormEvent } from 'react';
import { describeFileError, isNavigable, type FileEntry } from '../api/files';
import {
  DEFAULT_SEARCH_DEPTH,
  DEFAULT_SEARCH_RESULTS,
  MAX_SEARCH_DEPTH,
  MAX_SEARCH_RESULTS,
  describeSearchOutcome,
  type FileSearchEvent,
  type FileSearchMatch,
  type FileSearchMethod,
  type FileSearchQuery,
  type FileSearchSource,
  type FileSearchSummary,
  type FileSearchWarning,
} from '../api/fileSearch';
import { FileIcon, FolderIcon, LinkIcon } from './icons/Icons';
import { Spinner } from './Spinner';

export interface FileSearchPanelProps {
  title: string;
  search: FileSearchSource;
  rootPath: string;
  onOpenLocation: (match: FileSearchMatch) => void;
}

type SearchStatus = 'idle' | 'running' | 'stopped' | 'finished' | 'failed';

interface SearchState {
  status: SearchStatus;
  root: string;
  matches: FileSearchMatch[];
  warnings: FileSearchWarning[];
  notices: string[];
  summary: FileSearchSummary | null;
  error: string | null;
}

const IDLE_STATE: SearchState = {
  status: 'idle',
  root: '',
  matches: [],
  warnings: [],
  notices: [],
  summary: null,
  error: null,
};

function clampNumber(raw: FormDataEntryValue | null, fallback: number, upper: number): number {
  const value = Number(raw);
  if (!Number.isInteger(value) || value < 1) {
    return fallback;
  }
  return Math.min(value, upper);
}

function applyEvent(state: SearchState, event: FileSearchEvent): SearchState {
  switch (event.type) {
    case 'match':
      return { ...state, matches: [...state.matches, event] };
    case 'warning':
      return { ...state, warnings: [...state.warnings, event] };
    case 'notice':
      return { ...state, notices: [...state.notices, event.message] };
    case 'completed':
      return {
        ...state,
        status: event.summary.outcome === 'Failed' ? 'failed' : 'finished',
        summary: event.summary,
      };
  }
}

function EntryIcon({ entry }: { entry: FileEntry }) {
  if (entry.kind === 'SymbolicLink') {
    return <LinkIcon size={13} />;
  }
  return isNavigable(entry) ? <FolderIcon size={13} /> : <FileIcon size={13} />;
}

function StatusLine({ state }: { state: SearchState }) {
  if (state.status === 'idle') {
    return null;
  }
  if (state.status === 'running') {
    return (
      <span className="status-text status-text-active">
        <Spinner size={12} />
        Searching... {state.matches.length} found
      </span>
    );
  }
  if (state.status === 'stopped') {
    return (
      <span className="status-text">
        <span className="status-dot status-dot-sm status-dot-idle" aria-hidden="true" />
        Stopped. {state.matches.length} found, results are partial.
      </span>
    );
  }
  if (state.status === 'failed') {
    const message = state.error ?? (state.summary === null ? 'Search failed.' : describeSearchOutcome(state.summary));
    return (
      <span className="status-text status-text-danger">
        <span className="status-dot status-dot-sm status-dot-danger" aria-hidden="true" />
        Failed: {message}
      </span>
    );
  }
  const summary = state.summary;
  if (summary === null) {
    return null;
  }
  return (
    <span className={summary.isPartial ? 'status-text status-text-caution' : 'status-text status-text-ok'}>
      <span
        className={summary.isPartial ? 'status-dot status-dot-sm status-dot-caution' : 'status-dot status-dot-sm status-dot-ok'}
        aria-hidden="true"
      />
      {summary.isPartial ? 'Partial' : 'Complete'}: {describeSearchOutcome(summary)}
      {summary.depthLimitReached && ' Deeper folders were not searched.'}
    </span>
  );
}

export function FileSearchPanel({ title, search, rootPath, onOpenLocation }: FileSearchPanelProps) {
  const [state, setState] = useState<SearchState>(IDLE_STATE);
  const controllerRef = useRef<AbortController | null>(null);

  useEffect(
    () => () => {
      controllerRef.current?.abort();
    },
    [],
  );

  async function start(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const pattern = String(data.get('pattern') ?? '').trim();
    if (pattern.length === 0) {
      setState({ ...IDLE_STATE, status: 'failed', error: 'Enter a name pattern.' });
      return;
    }
    controllerRef.current?.abort();
    const controller = new AbortController();
    controllerRef.current = controller;
    const query: FileSearchQuery = {
      path: rootPath,
      pattern,
      maxDepth: clampNumber(data.get('maxDepth'), DEFAULT_SEARCH_DEPTH, MAX_SEARCH_DEPTH),
      maxResults: clampNumber(data.get('maxResults'), DEFAULT_SEARCH_RESULTS, MAX_SEARCH_RESULTS),
    };
    if (search.supportsRemoteFind) {
      query.method = String(data.get('method') ?? 'sftp') as FileSearchMethod;
    }
    setState({ ...IDLE_STATE, status: 'running', root: rootPath });
    try {
      await search.search(
        query,
        (searchEvent) => {
          if (!controller.signal.aborted) {
            setState((current) => applyEvent(current, searchEvent));
          }
        },
        controller.signal,
      );
      if (!controller.signal.aborted) {
        setState((current) =>
          current.status === 'running'
            ? { ...current, status: 'failed', error: 'The search ended without a result summary.' }
            : current,
        );
      }
    } catch (error) {
      if (!controller.signal.aborted) {
        setState((current) => ({ ...current, status: 'failed', error: describeFileError(error) }));
      }
    } finally {
      if (controllerRef.current === controller) {
        controllerRef.current = null;
      }
    }
  }

  function stop(): void {
    controllerRef.current?.abort();
    controllerRef.current = null;
    setState((current) => (current.status === 'running' ? { ...current, status: 'stopped' } : current));
  }

  const running = state.status === 'running';

  return (
    <div className="file-search" role="search" aria-label={`${title} file search`}>
      <form className="file-search-form" aria-label={`Search ${title} files`} onSubmit={(event) => void start(event)}>
        <label className="field file-search-pattern">
          <span className="field-label">Name pattern</span>
          <input
            className="input input-sm input-mono"
            name="pattern"
            placeholder="*.log"
            autoComplete="off"
            spellCheck={false}
            autoFocus
          />
        </label>
        <label className="field file-search-limit">
          <span className="field-label">Depth</span>
          <input
            className="input input-sm"
            name="maxDepth"
            type="number"
            min={1}
            max={MAX_SEARCH_DEPTH}
            defaultValue={DEFAULT_SEARCH_DEPTH}
          />
        </label>
        <label className="field file-search-limit">
          <span className="field-label">Max results</span>
          <input
            className="input input-sm"
            name="maxResults"
            type="number"
            min={1}
            max={MAX_SEARCH_RESULTS}
            defaultValue={DEFAULT_SEARCH_RESULTS}
          />
        </label>
        {search.supportsRemoteFind && (
          <label className="field file-search-method">
            <span className="field-label">Method</span>
            <select className="input input-sm" name="method" defaultValue="sftp">
              <option value="sftp">SFTP traversal</option>
              <option value="find">Remote find</option>
            </select>
          </label>
        )}
        {running ? (
          <button type="button" className="btn btn-sm" onClick={stop}>
            Stop
          </button>
        ) : (
          <button type="submit" className="btn btn-sm btn-filled">
            Search
          </button>
        )}
      </form>
      <p className="file-search-scope">
        Searches names under <code>{running || state.status !== 'idle' ? state.root : rootPath}</code>
      </p>
      <p role="status" className="file-search-status">
        <StatusLine state={state} />
      </p>
      {state.notices.map((notice) => (
        <p key={notice} className="notice notice-caution file-search-notice">
          {notice}
        </p>
      ))}
      {state.warnings.length > 0 && (
        <details className="file-search-warnings">
          <summary>
            {state.summary !== null && state.summary.warningCount > state.warnings.length
              ? `${state.summary.warningCount} locations could not be read (first ${state.warnings.length} shown)`
              : `${state.warnings.length} ${state.warnings.length === 1 ? 'location' : 'locations'} could not be read`}
          </summary>
          <ul>
            {state.warnings.map((warning, index) => (
              <li key={`${warning.path}|${index}`}>
                {warning.path.length > 0 && <code>{warning.path}</code>} {warning.message}
              </li>
            ))}
          </ul>
        </details>
      )}
      {state.status !== 'idle' && state.matches.length === 0 && !running && state.status !== 'failed' && (
        <p className="empty-state">No matching names.</p>
      )}
      {state.matches.length > 0 && (
        <ul className="file-search-results" aria-label={`${title} search results`}>
          {state.matches.map((match) => (
            <li key={match.entry.path} className="file-search-result">
              <span className={isNavigable(match.entry) ? 'file-icon file-icon-folder' : 'file-icon'}>
                <EntryIcon entry={match.entry} />
              </span>
              <span className="file-search-result-text">
                <span className="file-search-result-name">{match.entry.name}</span>
                <span className="file-search-result-path">{match.parentPath}</span>
              </span>
              <button
                type="button"
                className="btn btn-sm btn-ghost"
                aria-label={`Open location of ${match.entry.name}`}
                onClick={() => onOpenLocation(match)}
              >
                Open location
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

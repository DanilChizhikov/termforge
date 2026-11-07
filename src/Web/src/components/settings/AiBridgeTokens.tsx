import { useEffect, useRef, useState, type FormEvent } from 'react';
import {
  createAiBridgeToken,
  listAiBridgeTokens,
  revokeAiBridgeToken,
  type AiBridgeToken,
  type AiBridgeTokenInfo,
} from '../../api/aiBridge';
import { ApiError } from '../../api/client';
import { describeError } from '../../api/connections';
import { LoadingState } from '../LoadingState';
import { AiBridgeTokenSecret } from './AiBridgeTokenSecret';

export function AiBridgeTokens() {
  const [tokens, setTokens] = useState<AiBridgeTokenInfo[] | null>(null);
  const [name, setName] = useState('');
  const [created, setCreated] = useState<AiBridgeToken | null>(null);
  const [revoking, setRevoking] = useState<AiBridgeTokenInfo | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const active = useRef(false);

  useEffect(() => {
    active.current = true;
    let loading = true;
    listAiBridgeTokens()
      .then((loaded) => {
        if (loading) setTokens(loaded);
      })
      .catch((failure: unknown) => {
        if (loading) setError(describeError(failure));
      });
    return () => {
      active.current = false;
      loading = false;
    };
  }, []);

  const create = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (busy || tokens === null) return;
    setBusy(true);
    setError(null);
    setCreated(null);
    try {
      const generated = await createAiBridgeToken(name.trim());
      if (!active.current) return;
      const info = { id: generated.id, name: generated.name, createdAt: generated.createdAt };
      setTokens((current) => [...(current ?? []), info]);
      setCreated(generated);
      setName('');
    } catch (failure) {
      if (active.current) {
        setError(failure instanceof ApiError && failure.status === 409
          ? 'A token with this name already exists.' : describeError(failure));
      }
    } finally {
      if (active.current) setBusy(false);
    }
  };

  const revoke = async () => {
    if (revoking === null || busy) return;
    setBusy(true);
    setError(null);
    try {
      try {
        await revokeAiBridgeToken(revoking.id);
      } catch (failure) {
        if (!(failure instanceof ApiError && failure.status === 404)) throw failure;
      }
      if (!active.current) return;
      setTokens((current) => current?.filter((token) => token.id !== revoking.id) ?? []);
      if (created?.id === revoking.id) setCreated(null);
      setRevoking(null);
    } catch (failure) {
      if (active.current) setError(describeError(failure));
    } finally {
      if (active.current) setBusy(false);
    }
  };

  return (
    <>
      <div className="settings-row">
        <span className="settings-row-text">
          <span className="settings-row-title">Access tokens</span>
          <span className="settings-row-hint">Create a token for each agent. Server access and approvals apply to all tokens.</span>
        </span>
      </div>
      {tokens === null ? (
        error === null ? <LoadingState message="Loading access tokens…" className="settings-row" /> : null
      ) : (
        <>
          {tokens.length === 0 ? <p className="settings-row settings-row-hint">No access tokens yet.</p> : tokens.map((token) => (
            <div key={token.id} className="settings-row">
              <span className="settings-row-text ai-bridge-token-info">
                <span className="settings-row-title">{token.name}</span>
                <time className="settings-row-hint" dateTime={token.createdAt}>
                  Created {new Date(token.createdAt).toLocaleString()}
                </time>
              </span>
              <button type="button" className="btn btn-sm" disabled={busy} onClick={() => setRevoking(token)} aria-label={`Revoke ${token.name}`}>
                Revoke
              </button>
            </div>
          ))}
          <form className="settings-row ai-bridge-token-form" onSubmit={(event) => void create(event)}>
            <label className="field settings-row-text" htmlFor="ai-bridge-token-name">
              <span className="field-label">Token name</span>
              <input id="ai-bridge-token-name" className="input" value={name} onChange={(event) => setName(event.target.value)} maxLength={100} required disabled={busy} />
            </label>
            <button type="submit" className="btn btn-sm btn-primary" disabled={busy || name.trim().length === 0}>
              Create token
            </button>
          </form>
        </>
      )}
      {created === null ? null : <AiBridgeTokenSecret created={created} onClose={() => setCreated(null)} />}
      {error === null ? null : <p role="alert" className="settings-row form-error">{error}</p>}
      {revoking === null ? null : (
        <div className="dialog-backdrop">
          <div role="alertdialog" aria-modal="true" aria-labelledby="revoke-token-title" aria-describedby="revoke-token-description" className="dialog-card"
            onKeyDown={(event) => { if (event.key === 'Escape' && !busy) setRevoking(null); }}>
            <div className="dialog-header">
              <h2 id="revoke-token-title" className="dialog-title">Revoke {revoking.name}?</h2>
              <p id="revoke-token-description" className="text-secondary">
                This agent will lose access on its next request. Already accepted operations will continue.
              </p>
            </div>
            {error === null ? null : <p role="alert" className="dialog-body form-error">{error}</p>}
            <div className="dialog-footer">
              <button type="button" className="btn" autoFocus disabled={busy} onClick={() => setRevoking(null)}>Cancel</button>
              <button type="button" className="btn btn-destructive" disabled={busy} onClick={() => void revoke()}>Revoke token</button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}

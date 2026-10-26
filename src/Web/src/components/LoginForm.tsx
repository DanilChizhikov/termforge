import { useState, type FormEvent } from 'react';
import { isInsecureNetworkConnection, signIn, type SignInResult } from '../api/session';

export interface LoginFormProps {
  onSignedIn?: () => void;
}

function reloadPage(): void {
  window.location.reload();
}

function describeFailure(result: SignInResult): string | null {
  switch (result.kind) {
    case 'signed-in':
      return null;
    case 'wrong-password':
      return 'Wrong password.';
    case 'locked-out':
      return result.retryAfterSeconds === null
        ? 'Too many sign-in attempts. Try again later.'
        : `Too many sign-in attempts. Try again in ${result.retryAfterSeconds} seconds.`;
    case 'failed':
      return `Sign-in failed (HTTP ${result.status}).`;
  }
}

export function LoginForm({ onSignedIn = reloadPage }: LoginFormProps) {
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const insecure = isInsecureNetworkConnection(window.location);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      const result = await signIn(password);
      if (result.kind === 'signed-in') {
        setPassword('');
        onSignedIn();
        return;
      }
      setError(describeFailure(result));
    } catch {
      setError('Sign-in failed: the server is unreachable.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form className="login-form" aria-label="Sign in" onSubmit={(event) => void handleSubmit(event)}>
      <h1 className="dialog-title">Sign in to TermForge</h1>
      {insecure && (
        <p role="alert" className="notice notice-danger">
          Warning: this connection is not encrypted (HTTP). The password can be intercepted on the network.
        </p>
      )}
      <label className="field">
        <span className="field-label">Password</span>
        <input
          className="input"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          required
        />
      </label>
      <button type="submit" className="btn btn-primary" disabled={submitting || password.length === 0}>
        Sign in
      </button>
      {error !== null && (
        <p role="alert" className="form-error">
          {error}
        </p>
      )}
    </form>
  );
}

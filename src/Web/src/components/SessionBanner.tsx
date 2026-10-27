import { useEffect, useState } from 'react';
import { apiClient, subscribeSessionMissing } from '../api/client';
import { getLoginStatus, signOut } from '../api/session';
import { LoginForm } from './LoginForm';

type SessionState = 'unknown' | 'active' | 'password' | 'missing' | 'login';

export function SessionBanner() {
  const [state, setState] = useState<SessionState>('unknown');

  useEffect(() => {
    let disposed = false;
    const showMissing = () => {
      setState((current) => (current === 'login' ? current : 'missing'));
      void getLoginStatus().then((status) => {
        if (!disposed && status.passwordLoginEnabled) {
          setState('login');
        }
      });
    };
    const unsubscribe = subscribeSessionMissing(showMissing);
    apiClient
      .getSession()
      .then(() => getLoginStatus())
      .then((status) => {
        if (!disposed) {
          setState(status.signedInWithPassword ? 'password' : 'active');
        }
      })
      .catch(() => undefined);
    return () => {
      disposed = true;
      unsubscribe();
    };
  }, []);

  async function handleSignOut() {
    try {
      await signOut();
    } finally {
      window.location.reload();
    }
  }

  if (state === 'login') {
    return (
      <div className="session-banner session-banner-login">
        <LoginForm />
      </div>
    );
  }
  if (state === 'missing') {
    return (
      <div role="alert" className="session-banner">
        <span className="status-dot status-dot-sm status-dot-danger" aria-hidden="true" />
        Session expired or missing. Restart TermForge to open a new session.
      </div>
    );
  }
  if (state === 'password') {
    return (
      <div className="session-banner session-info">
        Signed in with password.{' '}
        <button type="button" className="btn btn-xs" onClick={() => void handleSignOut()}>
          Sign out
        </button>
      </div>
    );
  }
  return null;
}

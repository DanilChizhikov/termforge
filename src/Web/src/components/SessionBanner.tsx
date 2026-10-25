import { useEffect, useState } from 'react';
import { apiClient, subscribeSessionMissing } from '../api/client';

export function SessionBanner() {
  const [sessionMissing, setSessionMissing] = useState(false);

  useEffect(() => {
    const unsubscribe = subscribeSessionMissing(() => setSessionMissing(true));
    apiClient.getSession().catch(() => undefined);
    return unsubscribe;
  }, []);

  if (!sessionMissing) {
    return null;
  }
  return (
    <div role="alert" className="session-banner">
      Session expired or missing. Restart TermForge to open a new session.
    </div>
  );
}

import { useEffect, type ReactNode } from 'react';
import { useAuth } from 'react-oidc-context';
import { setRealtimeUserAccessor, startConnection, stopConnection } from './signalrClient';

export function RealtimeProvider({ children }: { children: ReactNode }) {
  const auth = useAuth();

  useEffect(() => {
    setRealtimeUserAccessor(() => auth.user);
  }, [auth.user]);

  useEffect(() => {
    if (!auth.isAuthenticated || !auth.user?.access_token) return;

    let cancelled = false;

    const connect = async () => {
      try {
        await startConnection();
      } catch (err) {
        if (!cancelled) {
          console.warn('[Realtime] Initial connection failed, will retry on reconnect:', err);
        }
      }
    };

    connect();

    return () => {
      cancelled = true;
      stopConnection().catch(() => {});
    };
  }, [auth.isAuthenticated, auth.user?.access_token]);

  return <>{children}</>;
}

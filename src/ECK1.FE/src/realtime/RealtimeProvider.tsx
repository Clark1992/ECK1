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
      const maxRetries = 4;
      const delays = [0, 2000, 5000, 10000];
      for (let i = 0; i < maxRetries; i++) {
        if (cancelled) return;
        try {
          await startConnection();
          return;
        } catch (err) {
          if (cancelled) return;
          if (i < maxRetries - 1) {
            console.warn(`[Realtime] Connection attempt ${i + 1} failed, retrying in ${delays[i + 1]}ms…`, err);
            await new Promise((r) => setTimeout(r, delays[i + 1]));
          } else {
            console.warn('[Realtime] All connection attempts failed:', err);
          }
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

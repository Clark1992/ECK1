import { useEffect, type ReactNode } from 'react';
import { AuthProvider as OidcAuthProvider } from 'react-oidc-context';
import type { WebStorageStateStore } from 'oidc-client-ts';
import { config } from '../config';
import { setUserAccessor } from '../api/client';
import { useAuth } from 'react-oidc-context';

const oidcConfig = {
  authority: config.ZITADEL_AUTHORITY,
  client_id: config.ZITADEL_CLIENT_ID,
  redirect_uri: `${window.location.origin}/auth/callback`,
  post_logout_redirect_uri: window.location.origin,
  scope: 'openid profile email urn:zitadel:iam:org:project:roles offline_access',
  response_type: 'code',
  automaticSilentRenew: true,
  userStore: undefined as unknown as WebStorageStateStore,
};

function TokenBridge({ children }: { children: ReactNode }) {
  const auth = useAuth();
  useEffect(() => {
    setUserAccessor(() => auth.user);
  }, [auth.user]);
  return <>{children}</>;
}

export function AuthProvider({ children }: { children: ReactNode }) {
  return (
    <OidcAuthProvider {...oidcConfig}>
      <TokenBridge>{children}</TokenBridge>
    </OidcAuthProvider>
  );
}

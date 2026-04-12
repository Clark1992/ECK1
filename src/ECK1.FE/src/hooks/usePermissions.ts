import { useAuth } from 'react-oidc-context';
import { useMemo } from 'react';

const ROLE_CLAIM = 'urn:zitadel:iam:org:project:roles';

const ROLE_PERMISSIONS: Record<string, string[]> = {
  admin: ['delete'],
};

export function usePermissions() {
  const auth = useAuth();

  return useMemo(() => {
    const roles = new Set<string>();
    const permissions = new Set<string>();

    if (auth.user?.profile) {
      const roleClaim = auth.user.profile[ROLE_CLAIM];
      if (roleClaim && typeof roleClaim === 'object') {
        for (const role of Object.keys(roleClaim as Record<string, unknown>)) {
          roles.add(role);
          const perms = ROLE_PERMISSIONS[role];
          if (perms) {
            perms.forEach((p) => permissions.add(p));
          }
        }
      }
    }

    return {
      roles,
      permissions,
      hasPermission: (p: string) => permissions.has(p),
      hasRole: (r: string) => roles.has(r),
      canDelete: permissions.has('delete'),
      isAuthenticated: auth.isAuthenticated,
    };
  }, [auth.user?.profile, auth.isAuthenticated]);
}

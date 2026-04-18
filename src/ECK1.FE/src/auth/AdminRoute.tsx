import { useAuth } from 'react-oidc-context';
import { type ReactNode } from 'react';
import { Box, Button, CircularProgress, Typography } from '@mui/material';
import { usePermissions } from '../hooks/usePermissions';

interface Props {
  children: ReactNode;
}

export default function AdminRoute({ children }: Props) {
  const auth = useAuth();
  const { hasRole } = usePermissions();

  if (auth.isLoading) {
    return (
      <Box display="flex" justifyContent="center" p={8}>
        <CircularProgress />
      </Box>
    );
  }

  if (!auth.isAuthenticated) {
    return (
      <Box display="flex" flexDirection="column" alignItems="center" gap={2} p={8}>
        <Typography variant="h6">Sign in required</Typography>
        <Button variant="contained" onClick={() => auth.signinRedirect()}>
          Sign in
        </Button>
      </Box>
    );
  }

  if (!hasRole('admin')) {
    return (
      <Box display="flex" flexDirection="column" alignItems="center" gap={2} p={8}>
        <Typography variant="h6">Access Denied</Typography>
        <Typography color="text.secondary">
          This page is restricted to administrators.
        </Typography>
      </Box>
    );
  }

  return <>{children}</>;
}

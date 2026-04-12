import { useAuth } from 'react-oidc-context';
import { type ReactNode } from 'react';
import { Box, Button, CircularProgress, Typography } from '@mui/material';

interface Props {
  children: ReactNode;
}

export default function ProtectedRoute({ children }: Props) {
  const auth = useAuth();

  if (auth.isLoading) {
    return (
      <Box display="flex" justifyContent="center" mt={8}>
        <CircularProgress />
      </Box>
    );
  }

  if (!auth.isAuthenticated) {
    return (
      <Box display="flex" flexDirection="column" alignItems="center" mt={8} gap={2}>
        <Typography variant="h5">Sign in required</Typography>
        <Typography color="text.secondary">
          You need to sign in to access this page.
        </Typography>
        <Button
          variant="contained"
          onClick={() => {
            sessionStorage.setItem('returnUrl', window.location.pathname);
            auth.signinRedirect();
          }}
        >
          Sign in
        </Button>
      </Box>
    );
  }

  return <>{children}</>;
}

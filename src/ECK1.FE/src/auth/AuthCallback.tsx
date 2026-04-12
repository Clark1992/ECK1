import { useAuth } from 'react-oidc-context';
import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Box, CircularProgress, Typography } from '@mui/material';

export default function AuthCallback() {
  const auth = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (!auth.isLoading && auth.isAuthenticated) {
      const returnUrl = sessionStorage.getItem('returnUrl') ?? '/';
      sessionStorage.removeItem('returnUrl');
      navigate(returnUrl, { replace: true });
    }
  }, [auth.isLoading, auth.isAuthenticated, navigate]);

  if (auth.error) {
    return (
      <Box display="flex" flexDirection="column" alignItems="center" mt={8}>
        <Typography color="error" variant="h6">
          Authentication error
        </Typography>
        <Typography color="text.secondary">{auth.error.message}</Typography>
      </Box>
    );
  }

  return (
    <Box display="flex" justifyContent="center" mt={8}>
      <CircularProgress />
    </Box>
  );
}

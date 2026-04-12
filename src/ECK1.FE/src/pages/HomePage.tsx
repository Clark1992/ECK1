import { useAuth } from 'react-oidc-context';
import { Box, Typography, Button, Paper, Grid2 as Grid, Card, CardContent, CardActions } from '@mui/material';
import ScienceIcon from '@mui/icons-material/Science';
import InventoryIcon from '@mui/icons-material/Inventory';
import { useNavigate } from 'react-router-dom';

export default function HomePage() {
  const auth = useAuth();
  const navigate = useNavigate();

  return (
    <Box>
      <Paper sx={{ p: 4, mb: 4, textAlign: 'center' }}>
        <Typography variant="h3" gutterBottom>
          ECK Platform
        </Typography>
        <Typography variant="h6" color="text.secondary" gutterBottom>
          Event-sourced CQRS platform with Kafka, Orleans, and Elasticsearch
        </Typography>
        {!auth.isAuthenticated && (
          <Button
            variant="contained"
            size="large"
            sx={{ mt: 2 }}
            onClick={() => {
              sessionStorage.setItem('returnUrl', '/');
              auth.signinRedirect();
            }}
          >
            Sign in to get started
          </Button>
        )}
      </Paper>

      {auth.isAuthenticated && (
        <Grid container spacing={3}>
          <Grid size={{ xs: 12, md: 6 }}>
            <Card>
              <CardContent>
                <Box display="flex" alignItems="center" gap={1} mb={1}>
                  <ScienceIcon color="primary" />
                  <Typography variant="h5">Samples</Typography>
                </Box>
                <Typography color="text.secondary">
                  Manage samples with names, descriptions, addresses, and attachments.
                </Typography>
              </CardContent>
              <CardActions>
                <Button onClick={() => navigate('/samples')}>View Samples</Button>
              </CardActions>
            </Card>
          </Grid>
          <Grid size={{ xs: 12, md: 6 }}>
            <Card>
              <CardContent>
                <Box display="flex" alignItems="center" gap={1} mb={1}>
                  <InventoryIcon color="primary" />
                  <Typography variant="h5">Orders (Sample2s)</Typography>
                </Box>
                <Typography color="text.secondary">
                  Manage orders with customers, shipping addresses, line items, tags, and statuses.
                </Typography>
              </CardContent>
              <CardActions>
                <Button onClick={() => navigate('/sample2s')}>View Orders (Sample2s)</Button>
              </CardActions>
            </Card>
          </Grid>
        </Grid>
      )}
    </Box>
  );
}

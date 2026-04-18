import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Box, Typography, Paper, TextField, Grid2 as Grid,
  CircularProgress, Alert, Chip, IconButton, Tooltip,
} from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import EditIcon from '@mui/icons-material/Edit';
import CheckIcon from '@mui/icons-material/Check';
import CloseIcon from '@mui/icons-material/Close';
import HistoryIcon from '@mui/icons-material/History';
import { samplesApi } from '../../api/samples';
import { useEntityUpdateFeedback } from '../../realtime/useRealtimeFeedback';
import { useEntityEvents } from '../../realtime/useEntityEvents';
import { useNotification } from '../../notifications/NotificationProvider';

export default function SampleDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [editingField, setEditingField] = useState<'name' | 'description' | null>(null);
  const [editValue, setEditValue] = useState('');
  const [error, setError] = useState('');

  const { data: response, isLoading, error: errorResponse } = useQuery({
    queryKey: ['samples', id],
    queryFn: () => samplesApi.get(id!),
    enabled: !!id,
  });

  const sample = response?.data;
  const isRebuilding = response?.isRebuilding ?? false;
  const showNotification = useNotification();

  // Realtime feedback for update operations — memorize version, show notification
  const { startCorrelatedCall, clearCorrelation, pendingVersionRef, clearPendingVersion, isPending } = useEntityUpdateFeedback(
    showNotification,
    (event) => setError(event?.message || event?.outcomeCode || 'Operation failed or timed out'),
  );

  // Subscribe to entity-level events (from any user/source) — auto-refetch on view update
  useEntityEvents({
    entityType: 'ECK1.Sample',
    entityId: id,
    onEvent: (event) => {
      queryClient.invalidateQueries({ queryKey: ['samples', id] });
      if (pendingVersionRef.current !== null && event.version >= pendingVersionRef.current) {
        clearPendingVersion();
        showNotification({
          title: event.title || 'Updated',
          message: event.message || 'View has been updated',
          severity: 'success',
        });
      }
    },
  });

  const nameMutation = useMutation({
    mutationFn: (name: string) => {
      const { correlationId } = startCorrelatedCall();
      return samplesApi.changeName(id!, name, sample?.version ?? 0, correlationId);
    },
    onSuccess: () => { setError(''); },
    onError: (err: Error) => { setError(err.message); clearCorrelation(); },
  });

  const descMutation = useMutation({
    mutationFn: (desc: string) => {
      const { correlationId } = startCorrelatedCall();
      return samplesApi.changeDescription(id!, desc, sample?.version ?? 0, correlationId);
    },
    onSuccess: () => { setError(''); },
    onError: (err: Error) => { setError(err.message); clearCorrelation(); },
  });

  const startEdit = (field: 'name' | 'description', value: string) => {
    setEditingField(field);
    setEditValue(value);
  };

  const submitEdit = () => {
    if (!editingField) return;
    const field = editingField;
    const value = editValue;
    setEditingField(null);
    if (field === 'name') nameMutation.mutate(value);
    else descMutation.mutate(value);
  };

  if (isLoading) {
    return <Box display="flex" justifyContent="center" mt={4}><CircularProgress /></Box>;
  }

  if (errorResponse) {
    return <Typography>Error!</Typography>;
  }

  if (!sample) {
    return <Typography>Sample not found</Typography>;
  }

  return (
    <Box>
      <Box display="flex" alignItems="center" gap={1} mb={3}>
        <IconButton onClick={() => navigate('/samples')}><ArrowBackIcon /></IconButton>
        <Typography variant="h4">Sample Details</Typography>
        <Typography variant="body2" color="text.secondary">({
          sample.lastModified 
            ? new Date(sample.lastModified).toLocaleString() 
            : '—'}
        )</Typography>
        <Tooltip title="View event history">
          <IconButton onClick={() => navigate(`/samples/${id}/history`)}><HistoryIcon /></IconButton>
        </Tooltip>
      </Box>

      {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError('')}>{error}</Alert>}
      {isPending && (
        <Alert severity="info" icon={<CircularProgress size={20} />} sx={{ mb: 2 }}>
          Processing — waiting for view to update…
        </Alert>
      )}
      {!isPending && isRebuilding && (
        <Alert severity="info" icon={<CircularProgress size={20} />} sx={{ mb: 2 }}>
          View is updating — displayed data may be stale.
        </Alert>
      )}

      <Grid container spacing={3}>
        <Grid size={{ xs: 12, md: 6 }}>
          <Paper sx={{ p: 3 }}>
            <Typography variant="h6" gutterBottom>General</Typography>

            {/* Name field */}
            <Box mb={2}>
              <Typography variant="caption" color="text.secondary">Name</Typography>
              {editingField === 'name' ? (
                <Box display="flex" gap={1}>
                  <TextField
                    size="small"
                    fullWidth
                    value={editValue}
                    onChange={(e) => setEditValue(e.target.value)}
                    onKeyDown={(e) => { if (e.key === 'Enter') submitEdit(); }}
                    autoFocus
                  />
                  <IconButton color="primary" onClick={submitEdit} disabled={nameMutation.isPending}><CheckIcon /></IconButton>
                  <IconButton onClick={() => setEditingField(null)}><CloseIcon /></IconButton>
                </Box>
              ) : (
                <Box display="flex" alignItems="center" gap={1}>
                  <Typography>{sample.name}</Typography>
                  <Tooltip title="Edit name">
                    <IconButton size="small" onClick={() => startEdit('name', sample.name)} disabled={isPending || isRebuilding}><EditIcon fontSize="small" /></IconButton>
                  </Tooltip>
                </Box>
              )}
            </Box>

            {/* Description field */}
            <Box mb={2}>
              <Typography variant="caption" color="text.secondary">Description</Typography>
              {editingField === 'description' ? (
                <Box display="flex" gap={1}>
                  <TextField
                    size="small"
                    fullWidth
                    multiline
                    rows={2}
                    value={editValue}
                    onChange={(e) => setEditValue(e.target.value)}
                    autoFocus
                  />
                  <IconButton color="primary" onClick={submitEdit} disabled={descMutation.isPending}><CheckIcon /></IconButton>
                  <IconButton onClick={() => setEditingField(null)}><CloseIcon /></IconButton>
                </Box>
              ) : (
                <Box display="flex" alignItems="center" gap={1}>
                  <Typography>{sample.description}</Typography>
                  <Tooltip title="Edit description">
                    <IconButton size="small" onClick={() => startEdit('description', sample.description)} disabled={isPending || isRebuilding}><EditIcon fontSize="small" /></IconButton>
                  </Tooltip>
                </Box>
              )}
            </Box>

            <Typography variant="overline" display="block" mt={2}>ID</Typography>
            <Typography variant="body2" color="text.secondary" sx={{ fontFamily: 'monospace' }}>{sample.sampleId}</Typography>
          </Paper>
        </Grid>

        <Grid size={{ xs: 12, md: 6 }}>
          <Paper sx={{ p: 3, mb: 3 }}>
            <Typography variant="h6" gutterBottom>Address</Typography>
            {sample.address ? (
              <Box>
                <Typography><strong>Street:</strong> {sample.address.street}</Typography>
                <Typography><strong>City:</strong> {sample.address.city}</Typography>
                <Typography><strong>Country:</strong> {sample.address.country}</Typography>
              </Box>
            ) : (
              <Typography color="text.secondary">No address</Typography>
            )}
          </Paper>

          <Paper sx={{ p: 3 }}>
            <Typography variant="h6" gutterBottom>Attachments</Typography>
            {sample.attachments && sample.attachments.length > 0 ? (
              <Box display="flex" flexWrap="wrap" gap={1}>
                {sample.attachments.map((a) => (
                  <Chip key={a.id} label={a.fileName} variant="outlined" />
                ))}
              </Box>
            ) : (
              <Typography color="text.secondary">No attachments</Typography>
            )}
          </Paper>
        </Grid>
      </Grid>
    </Box>
  );
}

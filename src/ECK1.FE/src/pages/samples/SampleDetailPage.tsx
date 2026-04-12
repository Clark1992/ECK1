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
import { samplesApi } from '../../api/samples';

export default function SampleDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [editingField, setEditingField] = useState<'name' | 'description' | null>(null);
  const [editValue, setEditValue] = useState('');
  const [error, setError] = useState('');

  const { data: sample, isLoading } = useQuery({
    queryKey: ['samples', id],
    queryFn: () => samplesApi.get(id!),
    enabled: !!id,
  });

  const nameMutation = useMutation({
    mutationFn: (name: string) => samplesApi.changeName(id!, name),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['samples', id] }); setEditingField(null); setError(''); },
    onError: (err: Error) => setError(err.message),
  });

  const descMutation = useMutation({
    mutationFn: (desc: string) => samplesApi.changeDescription(id!, desc),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['samples', id] }); setEditingField(null); setError(''); },
    onError: (err: Error) => setError(err.message),
  });

  const startEdit = (field: 'name' | 'description', value: string) => {
    setEditingField(field);
    setEditValue(value);
  };

  const submitEdit = () => {
    if (!editingField) return;
    if (editingField === 'name') nameMutation.mutate(editValue);
    else descMutation.mutate(editValue);
  };

  if (isLoading) {
    return <Box display="flex" justifyContent="center" mt={4}><CircularProgress /></Box>;
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
      </Box>

      {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError('')}>{error}</Alert>}

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
                    autoFocus
                  />
                  <IconButton color="primary" onClick={submitEdit} disabled={nameMutation.isPending}><CheckIcon /></IconButton>
                  <IconButton onClick={() => setEditingField(null)}><CloseIcon /></IconButton>
                </Box>
              ) : (
                <Box display="flex" alignItems="center" gap={1}>
                  <Typography>{sample.name}</Typography>
                  <Tooltip title="Edit name">
                    <IconButton size="small" onClick={() => startEdit('name', sample.name)}><EditIcon fontSize="small" /></IconButton>
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
                    <IconButton size="small" onClick={() => startEdit('description', sample.description)}><EditIcon fontSize="small" /></IconButton>
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

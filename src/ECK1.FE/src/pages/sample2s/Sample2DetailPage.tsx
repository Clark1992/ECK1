import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Box, Typography, Paper, TextField, Button, Grid2 as Grid,
  CircularProgress, Alert, Chip, IconButton, Tooltip,
  Table, TableBody, TableCell, TableHead, TableRow,
  MenuItem, Select, InputLabel, FormControl,
  Dialog, DialogTitle, DialogContent, DialogActions,
} from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import EditIcon from '@mui/icons-material/Edit';
import CheckIcon from '@mui/icons-material/Check';
import CloseIcon from '@mui/icons-material/Close';
import DeleteIcon from '@mui/icons-material/Delete';
import AddIcon from '@mui/icons-material/Add';
import { sample2sApi } from '../../api/sample2s';
import { Sample2Status, Sample2StatusLabels } from '../../types/sample2';
import { usePermissions } from '../../hooks/usePermissions';
import { useEntityUpdateFeedback } from '../../realtime/useRealtimeFeedback';
import { useEntityEvents } from '../../realtime/useEntityEvents';
import { useNotification } from '../../notifications/NotificationProvider';

export default function Sample2DetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { canDelete } = usePermissions();

  const [error, setError] = useState('');
  const [editingEmail, setEditingEmail] = useState(false);
  const [emailValue, setEmailValue] = useState('');
  const [editingAddress, setEditingAddress] = useState(false);
  const [addressValue, setAddressValue] = useState({ street: '', city: '', country: '' });
  const [statusDialog, setStatusDialog] = useState(false);
  const [newStatus, setNewStatus] = useState<Sample2Status>(Sample2Status.Draft);
  const [statusReason, setStatusReason] = useState('');
  const [addItemDialog, setAddItemDialog] = useState(false);
  const [newItem, setNewItem] = useState({ sku: '', quantity: '1', amount: '0', currency: 'USD' });
  const [addTagDialog, setAddTagDialog] = useState(false);
  const [newTag, setNewTag] = useState('');

  const { data: response, isLoading } = useQuery({
    queryKey: ['sample2s', id],
    queryFn: () => sample2sApi.get(id!),
    enabled: !!id,
  });

  const order = response?.data;
  const isRebuilding = response?.isRebuilding ?? false;
  const showNotification = useNotification();

  // Realtime feedback for update operations — memorize version, show notification
  const { startCorrelatedCall, clearCorrelation, pendingVersion, clearPendingVersion } = useEntityUpdateFeedback(
    queryClient, 'sample2s', id, showNotification,
    (event) => setError(event?.message || event?.outcomeCode || 'Operation failed or timed out'),
  );

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['sample2s', id] });

  // Subscribe to entity-level events (from any user/source) — auto-refetch on view update
  useEntityEvents({
    entityType: 'ECK1.Sample2',
    entityId: id,
    onEvent: (event) => {
      invalidate();
      if (pendingVersion !== null && event.version >= pendingVersion) {
        clearPendingVersion();
        showNotification({
          title: event.title || 'Updated',
          message: event.message || 'View has been updated',
          severity: 'success',
        });
      }
    },
  });

  const emailMutation = useMutation({
    mutationFn: (email: string) => {
      const { correlationId } = startCorrelatedCall();
      return sample2sApi.changeCustomerEmail(id!, email, order?.version ?? 0, correlationId);
    },
    onSuccess: () => { setEditingEmail(false); setError(''); },
    onError: (e: Error) => { setError(e.message); clearCorrelation(); },
  });

  const addressMutation = useMutation({
    mutationFn: (addr: { street: string; city: string; country: string }) => {
      const { correlationId } = startCorrelatedCall();
      return sample2sApi.changeShippingAddress(id!, addr, order?.version ?? 0, correlationId);
    },
    onSuccess: () => { setEditingAddress(false); setError(''); },
    onError: (e: Error) => { setError(e.message); clearCorrelation(); },
  });

  const statusMutation = useMutation({
    mutationFn: () => {
      const { correlationId } = startCorrelatedCall();
      return sample2sApi.changeStatus(id!, { newStatus, reason: statusReason }, order?.version ?? 0, correlationId);
    },
    onSuccess: () => { setStatusDialog(false); setStatusReason(''); setError(''); },
    onError: (e: Error) => { setError(e.message); clearCorrelation(); },
  });

  const addItemMutation = useMutation({
    mutationFn: () => {
      const { correlationId } = startCorrelatedCall();
      return sample2sApi.addLineItem(id!, {
        sku: newItem.sku,
        quantity: parseInt(newItem.quantity, 10) || 1,
        unitPrice: { amount: parseFloat(newItem.amount) || 0, currency: newItem.currency },
      }, order?.version ?? 0, correlationId);
    },
    onSuccess: () => {
      setAddItemDialog(false);
      setNewItem({ sku: '', quantity: '1', amount: '0', currency: 'USD' });
      setError('');
    },
    onError: (e: Error) => { setError(e.message); clearCorrelation(); },
  });

  const removeItemMutation = useMutation({
    mutationFn: (itemId: string) => {
      const { correlationId } = startCorrelatedCall();
      return sample2sApi.removeLineItem(id!, itemId, correlationId);
    },
    onSuccess: () => { setError(''); },
    onError: (e: Error) => { setError(e.message); clearCorrelation(); },
  });

  const addTagMutation = useMutation({
    mutationFn: (tag: string) => {
      const { correlationId } = startCorrelatedCall();
      return sample2sApi.addTag(id!, tag, order?.version ?? 0, correlationId);
    },
    onSuccess: () => { setAddTagDialog(false); setNewTag(''); setError(''); },
    onError: (e: Error) => { setError(e.message); clearCorrelation(); },
  });

  const removeTagMutation = useMutation({
    mutationFn: (tag: string) => {
      const { correlationId } = startCorrelatedCall();
      return sample2sApi.removeTag(id!, tag, correlationId);
    },
    onSuccess: () => { setError(''); },
    onError: (e: Error) => { setError(e.message); clearCorrelation(); },
  });

  if (isLoading) {
    return <Box display="flex" justifyContent="center" mt={4}><CircularProgress /></Box>;
  }
  if (!order) {
    return <Typography>Order not found</Typography>;
  }

  const statusColor: Record<number, 'default' | 'info' | 'warning' | 'success' | 'error'> = {
    0: 'default', 1: 'info', 2: 'warning', 3: 'success', 4: 'error',
  };

  return (
    <Box>
      <Box display="flex" alignItems="center" gap={1} mb={3}>
        <IconButton onClick={() => navigate('/sample2s')}><ArrowBackIcon /></IconButton>
        <Typography variant="h4">Order Details</Typography>
        <Chip
          label={Sample2StatusLabels[order.status]}
          color={statusColor[order.status] ?? 'default'}
          sx={{ ml: 1 }}
        />
        <Button size="small" variant="outlined" onClick={() => { setNewStatus(order.status); setStatusDialog(true); }} disabled={isRebuilding}>
          Change Status
        </Button>
        <Typography variant="body2" color="text.secondary">({
          order.lastModified 
            ? new Date(order.lastModified).toLocaleString() 
            : '—'}
        )</Typography>
      </Box>

      {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError('')}>{error}</Alert>}
      {isRebuilding && (
        <Alert severity="info" icon={<CircularProgress size={20} />} sx={{ mb: 2 }}>
          View is updating — displayed data may be stale.
        </Alert>
      )}

      <Grid container spacing={3}>
        {/* Customer */}
        <Grid size={{ xs: 12, md: 6 }}>
          <Paper sx={{ p: 3 }}>
            <Typography variant="h6" gutterBottom>Customer</Typography>
            <Box mb={2}>
              <Typography variant="caption" color="text.secondary">Email</Typography>
              {editingEmail ? (
                <Box display="flex" gap={1}>
                  <TextField size="small" fullWidth value={emailValue} onChange={(e) => setEmailValue(e.target.value)} autoFocus />
                  <IconButton color="primary" onClick={() => emailMutation.mutate(emailValue)} disabled={emailMutation.isPending}><CheckIcon /></IconButton>
                  <IconButton onClick={() => setEditingEmail(false)}><CloseIcon /></IconButton>
                </Box>
              ) : (
                <Box display="flex" alignItems="center" gap={1}>
                  <Typography>{order.customer?.email}</Typography>
                  <Tooltip title="Edit email"><IconButton size="small" disabled={isRebuilding} onClick={() => { setEmailValue(order.customer?.email ?? ''); setEditingEmail(true); }}><EditIcon fontSize="small" /></IconButton></Tooltip>
                </Box>
              )}
            </Box>
            <Typography variant="caption" color="text.secondary">Segment</Typography>
            <Typography>{order.customer?.segment}</Typography>
            <Typography variant="overline" display="block" mt={2}>ID</Typography>
            <Typography variant="body2" color="text.secondary" sx={{ fontFamily: 'monospace' }}>{order.sample2Id}</Typography>
          </Paper>
        </Grid>

        {/* Shipping Address */}
        <Grid size={{ xs: 12, md: 6 }}>
          <Paper sx={{ p: 3 }}>
            <Box display="flex" justifyContent="space-between" alignItems="center">
              <Typography variant="h6" gutterBottom>Shipping Address</Typography>
              {!editingAddress && (
                <Tooltip title="Edit address">
                  <IconButton size="small" disabled={isRebuilding} onClick={() => {
                    setAddressValue({
                      street: order.shippingAddress?.street ?? '',
                      city: order.shippingAddress?.city ?? '',
                      country: order.shippingAddress?.country ?? '',
                    });
                    setEditingAddress(true);
                  }}><EditIcon fontSize="small" /></IconButton>
                </Tooltip>
              )}
            </Box>
            {editingAddress ? (
              <Box display="flex" flexDirection="column" gap={1}>
                <TextField size="small" label="Street" value={addressValue.street} onChange={(e) => setAddressValue({ ...addressValue, street: e.target.value })} />
                <TextField size="small" label="City" value={addressValue.city} onChange={(e) => setAddressValue({ ...addressValue, city: e.target.value })} />
                <TextField size="small" label="Country" value={addressValue.country} onChange={(e) => setAddressValue({ ...addressValue, country: e.target.value })} />
                <Box display="flex" gap={1}>
                  <Button size="small" variant="contained" onClick={() => addressMutation.mutate(addressValue)} disabled={addressMutation.isPending}>Save</Button>
                  <Button size="small" onClick={() => setEditingAddress(false)}>Cancel</Button>
                </Box>
              </Box>
            ) : (
              <Box>
                <Typography><strong>Street:</strong> {order.shippingAddress?.street}</Typography>
                <Typography><strong>City:</strong> {order.shippingAddress?.city}</Typography>
                <Typography><strong>Country:</strong> {order.shippingAddress?.country}</Typography>
              </Box>
            )}
          </Paper>
        </Grid>

        {/* Line Items */}
        <Grid size={12}>
          <Paper sx={{ p: 3 }}>
            <Box display="flex" justifyContent="space-between" alignItems="center" mb={1}>
              <Typography variant="h6">Line Items</Typography>
              <Button size="small" startIcon={<AddIcon />} onClick={() => setAddItemDialog(true)} disabled={isRebuilding}>Add Item</Button>
            </Box>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>SKU</TableCell>
                  <TableCell>Quantity</TableCell>
                  <TableCell>Unit Price</TableCell>
                  <TableCell>Total</TableCell>
                  {canDelete && <TableCell />}
                </TableRow>
              </TableHead>
              <TableBody>
                {order.lineItems?.map((li) => (
                  <TableRow key={li.itemId}>
                    <TableCell>{li.sku}</TableCell>
                    <TableCell>{li.quantity}</TableCell>
                    <TableCell>{li.unitPrice.amount} {li.unitPrice.currency}</TableCell>
                    <TableCell>{(li.quantity * li.unitPrice.amount).toFixed(2)} {li.unitPrice.currency}</TableCell>
                    {canDelete && (
                      <TableCell>
                        <Tooltip title="Remove item">
                          <IconButton size="small" color="error" onClick={() => removeItemMutation.mutate(li.itemId)} disabled={removeItemMutation.isPending || isRebuilding}>
                            <DeleteIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      </TableCell>
                    )}
                  </TableRow>
                ))}
                {(!order.lineItems || order.lineItems.length === 0) && (
                  <TableRow><TableCell colSpan={canDelete ? 5 : 4} align="center">No items</TableCell></TableRow>
                )}
              </TableBody>
            </Table>
          </Paper>
        </Grid>

        {/* Tags */}
        <Grid size={12}>
          <Paper sx={{ p: 3 }}>
            <Box display="flex" justifyContent="space-between" alignItems="center" mb={1}>
              <Typography variant="h6">Tags</Typography>
              <Button size="small" startIcon={<AddIcon />} onClick={() => setAddTagDialog(true)} disabled={isRebuilding}>Add Tag</Button>
            </Box>
            <Box display="flex" flexWrap="wrap" gap={1}>
              {order.tags?.map((t) => (
                <Chip
                  key={t.value}
                  label={t.value}
                  onDelete={canDelete && !isRebuilding ? () => removeTagMutation.mutate(t.value) : undefined}
                />
              ))}
              {(!order.tags || order.tags.length === 0) && (
                <Typography color="text.secondary">No tags</Typography>
              )}
            </Box>
          </Paper>
        </Grid>
      </Grid>

      {/* Change Status Dialog */}
      <Dialog open={statusDialog} onClose={() => setStatusDialog(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Change Status</DialogTitle>
        <DialogContent>
          <Box display="flex" flexDirection="column" gap={2} mt={1}>
            <FormControl fullWidth>
              <InputLabel>Status</InputLabel>
              <Select value={newStatus} label="Status" onChange={(e) => setNewStatus(e.target.value as Sample2Status)}>
                {Object.entries(Sample2StatusLabels).map(([k, v]) => (
                  <MenuItem key={k} value={Number(k)}>{v}</MenuItem>
                ))}
              </Select>
            </FormControl>
            <TextField label="Reason" value={statusReason} onChange={(e) => setStatusReason(e.target.value)} multiline rows={2} />
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setStatusDialog(false)}>Cancel</Button>
          <Button variant="contained" onClick={() => statusMutation.mutate()} disabled={statusMutation.isPending}>Update</Button>
        </DialogActions>
      </Dialog>

      {/* Add Line Item Dialog */}
      <Dialog open={addItemDialog} onClose={() => setAddItemDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Add Line Item</DialogTitle>
        <DialogContent>
          <Box display="flex" flexDirection="column" gap={2} mt={1}>
            <TextField label="SKU" value={newItem.sku} onChange={(e) => setNewItem({ ...newItem, sku: e.target.value })} required />
            <TextField label="Quantity" type="number" value={newItem.quantity} onChange={(e) => setNewItem({ ...newItem, quantity: e.target.value })} />
            <Box display="flex" gap={2}>
              <TextField label="Price" type="number" value={newItem.amount} onChange={(e) => setNewItem({ ...newItem, amount: e.target.value })} fullWidth />
              <TextField label="Currency" value={newItem.currency} onChange={(e) => setNewItem({ ...newItem, currency: e.target.value })} sx={{ width: 120 }} />
            </Box>
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAddItemDialog(false)}>Cancel</Button>
          <Button variant="contained" onClick={() => addItemMutation.mutate()} disabled={!newItem.sku || addItemMutation.isPending}>Add</Button>
        </DialogActions>
      </Dialog>

      {/* Add Tag Dialog */}
      <Dialog open={addTagDialog} onClose={() => setAddTagDialog(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Add Tag</DialogTitle>
        <DialogContent>
          <TextField fullWidth label="Tag" value={newTag} onChange={(e) => setNewTag(e.target.value)} sx={{ mt: 1 }} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAddTagDialog(false)}>Cancel</Button>
          <Button variant="contained" onClick={() => addTagMutation.mutate(newTag)} disabled={!newTag || addTagMutation.isPending}>Add</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}

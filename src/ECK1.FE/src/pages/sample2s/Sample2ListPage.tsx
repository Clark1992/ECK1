import { useCallback, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Box, Typography, Button, TextField, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, Paper, TablePagination,
  TableSortLabel, IconButton, InputAdornment, Alert, Chip,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import SearchIcon from '@mui/icons-material/Search';
import ClearIcon from '@mui/icons-material/Clear';
import { sample2sApi } from '../../api/sample2s';
import { Sample2StatusLabels } from '../../types/sample2';
import type { CreateSample2Request } from '../../types/sample2';
import Sample2FormDialog from './Sample2FormDialog';
import { useCreateEntityFeedback } from '../../realtime/useRealtimeFeedback';
import { useNotification } from '../../notifications/NotificationProvider';

type OrderField = 'status' | 'customer.email' | 'lastModified';

const statusColors: Record<number, 'default' | 'info' | 'warning' | 'success' | 'error'> = {
  0: 'default',
  1: 'info',
  2: 'warning',
  3: 'success',
  4: 'error',
};

function formatDate(value: string | null | undefined): string {
  if (!value) return '—';
  const d = new Date(value);
  if (isNaN(d.getTime())) return '—';
  return d.toLocaleString();
}

export default function Sample2ListPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();

  const page = parseInt(searchParams.get('page') ?? '0', 10);
  const rowsPerPage = parseInt(searchParams.get('pageSize') ?? '10', 10);
  const orderField = (searchParams.get('sort') as OrderField) || 'lastModified';
  const orderDir = (searchParams.get('dir') as 'asc' | 'desc') || 'desc';
  const search = searchParams.get('q') ?? '';
  const searchInput = searchParams.get('q') ?? '';

  const updateParams = useCallback((updates: Record<string, string | null>) => {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      for (const [k, v] of Object.entries(updates)) {
        if (v === null || v === '') next.delete(k);
        else next.set(k, v);
      }
      return next;
    }, { replace: true });
  }, [setSearchParams]);

  const [createOpen, setCreateOpen] = [
    searchParams.get('create') === '1',
    (open: boolean) => updateParams({ create: open ? '1' : null }),
  ] as const;
  const [error, setError] = useState('');
  const showNotification = useNotification();

  // Realtime feedback for create operation
  const { startCorrelatedCall, clearCorrelation } = useCreateEntityFeedback(
    queryClient, 'sample2s', '/sample2s', showNotification,
    (event) => setError(event?.message || event?.outcomeCode || 'Operation failed or timed out'),
  );

  const orderParam = orderDir === 'desc' ? `-${orderField}` : orderField;
  const isSearch = search.length > 0;

  const { data, isLoading } = useQuery({
    queryKey: isSearch
      ? ['sample2s', 'search', { q: search, skip: page * rowsPerPage, top: rowsPerPage, order: orderParam }]
      : ['sample2s', 'list', { skip: page * rowsPerPage, top: rowsPerPage, order: orderParam }],
    queryFn: () =>
      isSearch
        ? sample2sApi.search({ q: search, skip: page * rowsPerPage, top: rowsPerPage, order: orderParam })
        : sample2sApi.list({ skip: page * rowsPerPage, top: rowsPerPage, order: orderParam }),
  });

  const createMutation = useMutation({
    mutationFn: (req: CreateSample2Request) => {
      const { correlationId } = startCorrelatedCall();
      return sample2sApi.create(req, correlationId);
    },
    onSuccess: () => {
      setCreateOpen(false);
    },
    onError: (err: Error) => {
      clearCorrelation();
      setError(err.message);
    },
  });

  const handleSort = useCallback((field: OrderField) => {
    const newDir = orderField === field && orderDir === 'asc' ? 'desc' : 'asc';
    updateParams({ sort: field, dir: newDir, page: '0' });
  }, [orderField, orderDir, updateParams]);

  const handleSearch = () => {
    updateParams({ q: (document.getElementById('sample2-search') as HTMLInputElement)?.value || null, page: '0' });
  };

  const handleClearSearch = () => {
    updateParams({ q: null, page: '0' });
  };

  return (
    <Box>
      <Box display="flex" justifyContent="space-between" alignItems="center" mb={2}>
        <Typography variant="h4">Orders (Sample2s)</Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCreateOpen(true)}>
          Create
        </Button>
      </Box>

      {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError('')}>{error}</Alert>}

      <Paper sx={{ mb: 2, p: 2 }}>
        <Box display="flex" gap={1}>
          <TextField
            id="sample2-search"
            size="small"
            placeholder="Search orders..."
            defaultValue={searchInput}
            onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
            fullWidth
            slotProps={{
              input: {
                startAdornment: <InputAdornment position="start"><SearchIcon /></InputAdornment>,
                endAdornment: search && (
                  <InputAdornment position="end">
                    <IconButton size="small" onClick={handleClearSearch}>
                      <ClearIcon />
                    </IconButton>
                  </InputAdornment>
                ),
              },
            }}
          />
          <Button variant="outlined" onClick={handleSearch}>Search</Button>
        </Box>
      </Paper>

      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>
                <TableSortLabel active={orderField === 'customer.email'} direction={orderField === 'customer.email' ? orderDir : 'asc'} onClick={() => handleSort('customer.email')}>
                  Customer
                </TableSortLabel>
              </TableCell>
              <TableCell>Shipping Address</TableCell>
              <TableCell>
                <TableSortLabel active={orderField === 'status'} direction={orderField === 'status' ? orderDir : 'asc'} onClick={() => handleSort('status')}>
                  Status
                </TableSortLabel>
              </TableCell>
              <TableCell>Items</TableCell>
              <TableCell>Tags</TableCell>
              <TableCell>
                <TableSortLabel active={orderField === 'lastModified'} direction={orderField === 'lastModified' ? orderDir : 'asc'} onClick={() => handleSort('lastModified')}>
                  Last Modified
                </TableSortLabel>
              </TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading && (
              <TableRow><TableCell colSpan={6} align="center">Loading...</TableCell></TableRow>
            )}
            {data?.items.map((s) => (
              <TableRow
                key={s.sample2Id}
                hover
                sx={{ cursor: 'pointer' }}
                onClick={() => navigate(`/sample2s/${s.sample2Id}`)}
              >
                <TableCell>
                  <Typography variant="body2">{s.customer?.email}</Typography>
                  <Typography variant="caption" color="text.secondary">{s.customer?.segment}</Typography>
                </TableCell>
                <TableCell>
                  {s.shippingAddress
                    ? `${s.shippingAddress.street}, ${s.shippingAddress.city}`
                    : '—'}
                </TableCell>
                <TableCell>
                  <Chip label={Sample2StatusLabels[s.status] ?? s.status} size="small" color={statusColors[s.status] ?? 'default'} />
                </TableCell>
                <TableCell>{s.lineItems?.length ?? 0}</TableCell>
                <TableCell>
                  <Box display="flex" gap={0.5} flexWrap="wrap">
                    {s.tags?.slice(0, 3).map((t) => (
                      <Chip key={t.value} label={t.value} size="small" variant="outlined" />
                    ))}
                    {(s.tags?.length ?? 0) > 3 && <Chip label={`+${s.tags.length - 3}`} size="small" />}
                  </Box>
                </TableCell>
                <TableCell>{formatDate(s.lastModified)}</TableCell>
              </TableRow>
            ))}
            {!isLoading && data?.items.length === 0 && (
              <TableRow><TableCell colSpan={6} align="center">No orders found</TableCell></TableRow>
            )}
          </TableBody>
        </Table>
        <TablePagination
          component="div"
          count={data?.total ?? 0}
          page={page}
          rowsPerPage={rowsPerPage}
          onPageChange={(_, p) => updateParams({ page: String(p) })}
          onRowsPerPageChange={(e) => updateParams({ pageSize: e.target.value, page: '0' })}
        />
      </TableContainer>

      <Sample2FormDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onSubmit={(req) => createMutation.mutate(req)}
        loading={createMutation.isPending}
      />
    </Box>
  );
}

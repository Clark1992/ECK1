import { useCallback } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Box, Typography, Button, TextField, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, Paper, TablePagination,
  TableSortLabel, IconButton, InputAdornment, Alert,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import SearchIcon from '@mui/icons-material/Search';
import ClearIcon from '@mui/icons-material/Clear';
import { samplesApi } from '../../api/samples';
import SampleFormDialog from './SampleFormDialog';
import type { CreateSampleRequest } from '../../types/sample';

type OrderField = 'name' | 'description' | 'lastModified';

function formatDate(value: string | null | undefined): string {
  if (!value) return '—';
  const d = new Date(value);
  if (isNaN(d.getTime())) return '—';
  return d.toLocaleString();
}

export default function SampleListPage() {
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
  const [error, setError] = ['', (_v: string) => {}] as [string, (v: string) => void];

  const orderParam = orderDir === 'desc' ? `-${orderField}` : orderField;
  const isSearch = search.length > 0;

  const { data, isLoading } = useQuery({
    queryKey: isSearch
      ? ['samples', 'search', { q: search, skip: page * rowsPerPage, top: rowsPerPage, order: orderParam }]
      : ['samples', 'list', { skip: page * rowsPerPage, top: rowsPerPage, order: orderParam }],
    queryFn: () =>
      isSearch
        ? samplesApi.search({ q: search, skip: page * rowsPerPage, top: rowsPerPage, order: orderParam })
        : samplesApi.list({ skip: page * rowsPerPage, top: rowsPerPage, order: orderParam }),
  });

  const createMutation = useMutation({
    mutationFn: (req: CreateSampleRequest) => samplesApi.create(req),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['samples'] });
      setCreateOpen(false);
    },
    onError: (err: Error) => setError(err.message),
  });

  const handleSort = useCallback((field: OrderField) => {
    const newDir = orderField === field && orderDir === 'asc' ? 'desc' : 'asc';
    updateParams({ sort: field, dir: newDir, page: '0' });
  }, [orderField, orderDir, updateParams]);

  const handleSearch = () => {
    updateParams({ q: (document.getElementById('sample-search') as HTMLInputElement)?.value || null, page: '0' });
  };

  const handleClearSearch = () => {
    updateParams({ q: null, page: '0' });
  };

  return (
    <Box>
      <Box display="flex" justifyContent="space-between" alignItems="center" mb={2}>
        <Typography variant="h4">Samples</Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCreateOpen(true)}>
          Create
        </Button>
      </Box>

      {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError('')}>{error}</Alert>}

      <Paper sx={{ mb: 2, p: 2 }}>
        <Box display="flex" gap={1}>
          <TextField
            id="sample-search"
            size="small"
            placeholder="Search samples..."
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
                <TableSortLabel active={orderField === 'name'} direction={orderField === 'name' ? orderDir : 'asc'} onClick={() => handleSort('name')}>
                  Name
                </TableSortLabel>
              </TableCell>
              <TableCell>
                <TableSortLabel active={orderField === 'description'} direction={orderField === 'description' ? orderDir : 'asc'} onClick={() => handleSort('description')}>
                  Description
                </TableSortLabel>
              </TableCell>
              <TableCell>Address</TableCell>
              <TableCell>Attachments</TableCell>
              <TableCell>
                <TableSortLabel active={orderField === 'lastModified'} direction={orderField === 'lastModified' ? orderDir : 'asc'} onClick={() => handleSort('lastModified')}>
                  Last Modified
                </TableSortLabel>
              </TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading && (
              <TableRow><TableCell colSpan={5} align="center">Loading...</TableCell></TableRow>
            )}
            {data?.items.map((s) => (
              <TableRow
                key={s.sampleId}
                hover
                sx={{ cursor: 'pointer' }}
                onClick={() => navigate(`/samples/${s.sampleId}`)}
              >
                <TableCell>{s.name}</TableCell>
                <TableCell>{s.description}</TableCell>
                <TableCell>
                  {s.address ? `${s.address.street}, ${s.address.city}, ${s.address.country}` : '—'}
                </TableCell>
                <TableCell>{s.attachments?.length ?? 0}</TableCell>
                <TableCell>{formatDate(s.lastModified)}</TableCell>
              </TableRow>
            ))}
            {!isLoading && data?.items.length === 0 && (
              <TableRow><TableCell colSpan={5} align="center">No samples found</TableCell></TableRow>
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

      <SampleFormDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onSubmit={(req) => createMutation.mutate(req)}
        loading={createMutation.isPending}
      />
    </Box>
  );
}

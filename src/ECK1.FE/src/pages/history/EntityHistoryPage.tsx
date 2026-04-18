import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import {
  Box, Typography, Paper, IconButton, CircularProgress,
  Table, TableHead, TableRow, TableCell, TableBody,
  Chip, Collapse, Alert,
} from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import ExpandLessIcon from '@mui/icons-material/ExpandLess';
import { historyApi } from '../../api/history';
import type { EntityHistoryEvent } from '../../types/history';

interface EntityHistoryPageProps {
  entityLabel: string;
  backPath: string;
  fetchHistory: (id: string) => ReturnType<typeof historyApi.getSampleHistory>;
}

function HistoryEventRow({ event }: { event: EntityHistoryEvent }) {
  const [expanded, setExpanded] = useState(false);

  let parsedPayload: string | null = null;
  try {
    parsedPayload = JSON.stringify(JSON.parse(event.payload), null, 2);
  } catch {
    parsedPayload = event.payload;
  }

  return (
    <>
      <TableRow hover sx={{ cursor: 'pointer' }} onClick={() => setExpanded(!expanded)}>
        <TableCell>{event.entityVersion}</TableCell>
        <TableCell>
          <Chip label={event.eventType} size="small" variant="outlined" />
        </TableCell>
        <TableCell>{new Date(event.occurredAt).toLocaleString()}</TableCell>
        <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.75rem' }}>{event.eventId}</TableCell>
        <TableCell>
          <IconButton size="small">
            {expanded ? <ExpandLessIcon /> : <ExpandMoreIcon />}
          </IconButton>
        </TableCell>
      </TableRow>
      <TableRow>
        <TableCell colSpan={5} sx={{ py: 0, borderBottom: expanded ? undefined : 'none' }}>
          <Collapse in={expanded} timeout="auto" unmountOnExit>
            <Box sx={{ py: 2 }}>
              <Typography variant="caption" color="text.secondary" gutterBottom display="block">
                Payload
              </Typography>
              <Paper variant="outlined" sx={{ p: 2, bgcolor: 'grey.50', overflow: 'auto', maxHeight: 300 }}>
                <pre style={{ margin: 0, whiteSpace: 'pre-wrap', wordBreak: 'break-word', fontSize: '0.8rem' }}>
                  {parsedPayload}
                </pre>
              </Paper>
            </Box>
          </Collapse>
        </TableCell>
      </TableRow>
    </>
  );
}

function EntityHistoryPageInner({ entityLabel, backPath, fetchHistory }: EntityHistoryPageProps) {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const { data, isLoading, error } = useQuery({
    queryKey: ['history', entityLabel, id],
    queryFn: () => fetchHistory(id!),
    enabled: !!id,
  });

  if (isLoading) {
    return <Box display="flex" justifyContent="center" mt={4}><CircularProgress /></Box>;
  }

  if (error) {
    return <Alert severity="error">Failed to load history</Alert>;
  }

  const events = data?.events ?? [];

  return (
    <Box>
      <Box display="flex" alignItems="center" gap={1} mb={3}>
        <IconButton onClick={() => navigate(`${backPath}/${id}`)}><ArrowBackIcon /></IconButton>
        <Typography variant="h4">{entityLabel} History</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ fontFamily: 'monospace' }}>
          ({id})
        </Typography>
      </Box>

      {events.length === 0 ? (
        <Typography color="text.secondary">No history events found.</Typography>
      ) : (
        <>
          <Typography variant="body2" color="text.secondary" mb={2}>
            {events.length} event{events.length !== 1 ? 's' : ''}
          </Typography>
          <Paper>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Version</TableCell>
                  <TableCell>Event Type</TableCell>
                  <TableCell>Occurred At</TableCell>
                  <TableCell>Event ID</TableCell>
                  <TableCell />
                </TableRow>
              </TableHead>
              <TableBody>
                {events.map((event) => (
                  <HistoryEventRow key={event.eventId} event={event} />
                ))}
              </TableBody>
            </Table>
          </Paper>
        </>
      )}
    </Box>
  );
}

export default function SampleHistoryPage() {
  return (
    <EntityHistoryPageInner
      entityLabel="Sample"
      backPath="/samples"
      fetchHistory={(id) => historyApi.getSampleHistory(id)}
    />
  );
}

export function Sample2HistoryPage() {
  return (
    <EntityHistoryPageInner
      entityLabel="Sample2"
      backPath="/sample2s"
      fetchHistory={(id) => historyApi.getSample2History(id)}
    />
  );
}

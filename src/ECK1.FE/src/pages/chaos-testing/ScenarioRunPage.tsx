import { useParams, useLocation, useNavigate } from 'react-router-dom';
import { useMutation, useQuery } from '@tanstack/react-query';
import {
  Box,
  Typography,
  Stepper,
  Step,
  StepLabel,
  StepContent,
  Paper,
  Chip,
  Alert,
  CircularProgress,
  Button,
  Table,
  TableBody,
  TableRow,
  TableCell,
} from '@mui/material';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import CancelIcon from '@mui/icons-material/Cancel';
import ErrorIcon from '@mui/icons-material/Error';
import HourglassEmptyIcon from '@mui/icons-material/HourglassEmpty';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import { ApiError } from '../../api/client';
import { useScenarioProgress } from '../../hooks/useScenarioProgress';
import { testplatformApi, type StepProgress, type ScenarioProgress } from '../../api/testplatform';

export default function ScenarioRunPage() {
  const { runId } = useParams<{ runId: string }>();
  const location = useLocation();
  const navigate = useNavigate();
  const state = location.state as {
    scenarioName?: string;
    resolvedSettings?: Record<string, unknown>;
    stepNames?: string[];
  } | null;

  // Load progress from API (works for historical + initial state for live runs)
  const { data: apiProgress } = useQuery({
    queryKey: ['run-progress', runId],
    queryFn: () => testplatformApi.getRunProgress(runId!),
    enabled: !!runId,
    refetchInterval: (query) => {
      // Stop polling once completed or SignalR is connected
      const data = query.state.data as ScenarioProgress | undefined;
      if (data?.isCompleted) return false;
      return 2000;
    },
  });

  // Live updates via SignalR (for active runs)
  const { progress: signalRProgress, connected } = useScenarioProgress(runId ?? null);

  const cancelRunMutation = useMutation({
    mutationFn: () => testplatformApi.cancelRun(runId!),
  });

  // Prefer SignalR data (most up-to-date), fall back to API data
  const progress = signalRProgress ?? apiProgress ?? null;

  const scenarioName = progress?.scenarioName ?? state?.scenarioName ?? 'Scenario';
  const settings = state?.resolvedSettings ?? (progress ? extractSettings(progress) : undefined);
  const steps = progress?.steps;
  const isCompleted = progress?.isCompleted ?? false;
  const isSuccess = progress?.isSuccess ?? false;
  const isCancelled = progress?.isCancelled ?? false;

  const activeStepIndex = steps
    ? steps.findIndex((s) => s.status === 'InProgress')
    : -1;

  return (
    <Box>
      <Button
        startIcon={<ArrowBackIcon />}
        onClick={() => navigate('/chaos-testing')}
        sx={{ mb: 2 }}
      >
        Back to Scenarios
      </Button>

      <Typography variant="h4" gutterBottom>{scenarioName}</Typography>

      {runId && !isCompleted && (
        <Box sx={{ mb: 2, display: 'flex', gap: 1 }}>
          <Button
            variant="outlined"
            color="warning"
            startIcon={cancelRunMutation.isPending ? <CircularProgress size={16} /> : <CancelIcon />}
            disabled={cancelRunMutation.isPending || cancelRunMutation.isSuccess}
            onClick={() => cancelRunMutation.mutate()}
          >
            {cancelRunMutation.isPending
              ? 'Cancelling...'
              : cancelRunMutation.isSuccess
                ? 'Cancellation Requested'
                : 'Cancel Run'}
          </Button>
        </Box>
      )}

      {!connected && !isCompleted && !progress && (
        <Alert severity="info" sx={{ mb: 2 }}>
          <CircularProgress size={14} sx={{ mr: 1 }} />
          Loading scenario progress...
        </Alert>
      )}

      {cancelRunMutation.isSuccess && !isCompleted && (
        <Alert severity="info" sx={{ mb: 2 }}>
          Cancellation requested. Waiting for the run to stop and finish cleanup...
        </Alert>
      )}

      {cancelRunMutation.isError && !isCompleted && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          Failed to cancel run: {getErrorMessage(cancelRunMutation.error)}
        </Alert>
      )}

      {settings && (
        <Paper variant="outlined" sx={{ p: 2, mb: 3 }}>
          <Typography variant="subtitle2" gutterBottom>Run Settings</Typography>
          <Table size="small">
            <TableBody>
              {Object.entries(settings).map(([key, val]) => (
                <TableRow key={key}>
                  <TableCell sx={{ fontWeight: 500, width: '40%' }}>{key}</TableCell>
                  <TableCell>{String(val)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Paper>
      )}

      {isCompleted && (
        <Alert severity={isSuccess ? 'success' : isCancelled ? 'warning' : 'error'} sx={{ mb: 2 }}>
          {isSuccess
            ? 'Scenario completed successfully — all entities self-healed.'
            : isCancelled
              ? `Scenario cancelled: ${progress?.error ?? 'Cleanup completed.'}`
              : `Scenario failed: ${progress?.error ?? 'Unknown error'}`}
          {progress?.completedAt && (
            <Typography variant="caption" display="block" sx={{ mt: 0.5 }}>
              Completed at {new Date(progress.completedAt).toLocaleTimeString()}
              {progress.startedAt && ` (duration: ${formatDuration(progress.startedAt, progress.completedAt)})`}
            </Typography>
          )}
        </Alert>
      )}

      {steps ? (
        <Stepper
          activeStep={isCompleted ? steps.length : activeStepIndex}
          orientation="vertical"
        >
          {steps.map((step) => (
            <Step
              key={step.stepIndex}
              completed={step.status === 'Completed'}
              expanded={step.status !== 'Pending'}
            >
              <StepLabel
                error={step.status === 'Failed'}
                icon={<StepIcon status={step.status} />}
                sx={{
                  '& .MuiStepLabel-label': {
                    color: step.status === 'Completed' ? 'success.main'
                      : step.status === 'Cancelled' ? 'warning.main'
                      : step.status === 'Failed' ? 'error.main'
                      : undefined,
                  },
                }}
              >
                <Box display="flex" alignItems="center" gap={1}>
                  {step.stepName}
                  <StatusChip status={step.status} />
                  {step.startedAt && (
                    <Typography variant="caption" color="text.secondary">
                      {step.completedAt
                        ? formatDuration(step.startedAt, step.completedAt)
                        : `${formatElapsed(step.startedAt)}...`}
                    </Typography>
                  )}
                </Box>
              </StepLabel>
              <StepContent>
                {step.message && (
                  <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
                    {step.message}
                  </Typography>
                )}
                {step.data && <StepDataView data={step.data} />}
              </StepContent>
            </Step>
          ))}
        </Stepper>
      ) : state?.stepNames ? (
        <Stepper activeStep={-1} orientation="vertical">
          {state.stepNames.map((name, i) => (
            <Step key={i}>
              <StepLabel>{name}</StepLabel>
            </Step>
          ))}
        </Stepper>
      ) : (
        <Box display="flex" alignItems="center" gap={1} sx={{ mt: 2 }}>
          <CircularProgress size={20} />
          <Typography>Waiting for scenario progress...</Typography>
        </Box>
      )}
    </Box>
  );
}

function StepIcon({ status }: { status: StepProgress['status'] }) {
  switch (status) {
    case 'Completed': return <CheckCircleIcon color="success" />;
    case 'Cancelled': return <CancelIcon color="warning" />;
    case 'Failed': return <ErrorIcon color="error" />;
    case 'InProgress': return <CircularProgress size={20} />;
    default: return <HourglassEmptyIcon color="disabled" />;
  }
}

function StatusChip({ status }: { status: StepProgress['status'] }) {
  const colorMap: Record<string, 'default' | 'success' | 'error' | 'info' | 'warning'> = {
    Pending: 'default',
    InProgress: 'info',
    Completed: 'success',
    Failed: 'error',
    Cancelled: 'warning',
  };
  return <Chip label={status} size="small" color={colorMap[status] ?? 'default'} variant="outlined" />;
}

function getErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    return error.body || error.message;
  }

  if (error instanceof Error) {
    return error.message;
  }

  return 'Unknown error';
}

function StepDataView({ data }: { data: Record<string, unknown> }) {
  const entries = Object.entries(data).filter(
    ([, v]) => !Array.isArray(v) || (v as unknown[]).length <= 10,
  );
  if (entries.length === 0) return null;

  return (
    <Paper variant="outlined" sx={{ p: 1.5, mt: 1, bgcolor: 'action.hover' }}>
      <Table size="small">
        <TableBody>
          {entries.map(([key, val]) => (
            <TableRow key={key}>
              <TableCell sx={{ fontWeight: 500, width: '40%', border: 'none', py: 0.5 }}>{key}</TableCell>
              <TableCell sx={{ border: 'none', py: 0.5 }}>
                {Array.isArray(val)
                  ? val.length === 0
                    ? '(none)'
                    : val.map((v) => String(v)).join(', ')
                  : typeof val === 'object' && val !== null
                    ? JSON.stringify(val)
                    : String(val)}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Paper>
  );
}

function formatDuration(startIso: string, endIso: string): string {
  const ms = new Date(endIso).getTime() - new Date(startIso).getTime();
  const seconds = Math.floor(ms / 1000);
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  const remaining = seconds % 60;
  return `${minutes}m ${remaining}s`;
}

function formatElapsed(startIso: string): string {
  const ms = Date.now() - new Date(startIso).getTime();
  const seconds = Math.floor(ms / 1000);
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  const remaining = seconds % 60;
  return `${minutes}m ${remaining}s`;
}

function extractSettings(progress: ScenarioProgress): Record<string, unknown> | undefined {
  return progress.settings ?? undefined;
}

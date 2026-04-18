import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import {
  Box,
  Card,
  CardContent,
  CardActions,
  Typography,
  Button,
  TextField,
  MenuItem,
  Chip,
  Slider,
  FormControl,
  InputLabel,
  Select,
  OutlinedInput,
  CircularProgress,
  Alert,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  type SelectChangeEvent,
} from '@mui/material';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import ScienceIcon from '@mui/icons-material/Science';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import ErrorIcon from '@mui/icons-material/Error';
import HourglassEmptyIcon from '@mui/icons-material/HourglassEmpty';
import { testplatformApi, type ScenarioDefinition, type ScenarioSettingDefinition } from '../../api/testplatform';
import { ApiError } from '../../api/client';

export default function ScenarioListPage() {
  const navigate = useNavigate();
  const { data: scenarios, isLoading, error } = useQuery({
    queryKey: ['scenarios'],
    queryFn: testplatformApi.listScenarios,
  });

  const { data: recentRuns } = useQuery({
    queryKey: ['recent-runs'],
    queryFn: () => testplatformApi.getRecentRuns(20),
    refetchInterval: 10000,
  });

  const [selected, setSelected] = useState<ScenarioDefinition | null>(null);
  const [settings, setSettings] = useState<Record<string, unknown>>({});
  const [running, setRunning] = useState(false);
  const [runError, setRunError] = useState<string | null>(null);

  const openConfig = (scenario: ScenarioDefinition) => {
    const defaults: Record<string, unknown> = {};
    for (const s of scenario.settings) {
      defaults[s.key] = s.defaultValue;
    }
    setSettings(defaults);
    setRunError(null);
    setSelected(scenario);
  };

  const handleRun = async () => {
    if (!selected) return;
    setRunning(true);
    setRunError(null);
    try {
      const result = await testplatformApi.runScenario(selected.id, settings);
      setSelected(null);
      navigate(`/chaos-testing/run/${result.runId}`, {
        state: {
          scenarioName: result.scenarioName,
          resolvedSettings: result.resolvedSettings,
          stepNames: result.stepNames,
        },
      });
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        try {
          const body = JSON.parse(err.body);
          setRunError(`Another scenario is already running (run: ${body.activeRunId})`);
        } catch {
          setRunError('Another scenario is already running');
        }
      } else {
        console.error('Failed to start scenario:', err);
        setRunError(err instanceof Error ? err.message : 'Failed to start scenario');
      }
      setRunning(false);
    }
  };

  if (isLoading) return <CircularProgress sx={{ mt: 4 }} />;
  if (error) return <Alert severity="error" sx={{ mt: 2 }}>Failed to load scenarios</Alert>;

  return (
    <Box>
      <Typography variant="h4" gutterBottom sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
        <ScienceIcon fontSize="large" />
        Chaos Testing
      </Typography>
      <Typography variant="body1" color="text.secondary" sx={{ mb: 3 }}>
        Select a resilience scenario to reproduce and observe self-healing behavior.
      </Typography>

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' }, gap: 2 }}>
        {scenarios?.map((scenario) => (
          <Card key={scenario.id} variant="outlined">
            <CardContent>
              <Typography variant="h6" gutterBottom>{scenario.name}</Typography>
              <Typography variant="body2" color="text.secondary">{scenario.description}</Typography>
              <Box sx={{ mt: 1 }}>
                <Typography variant="caption" color="text.secondary">
                  {scenario.stepNames.length} steps
                </Typography>
              </Box>
            </CardContent>
            <CardActions>
              <Button
                startIcon={<PlayArrowIcon />}
                onClick={() => openConfig(scenario)}
                variant="contained"
                size="small"
              >
                Configure & Run
              </Button>
            </CardActions>
          </Card>
        ))}
      </Box>

      {recentRuns && recentRuns.length > 0 && (
        <Box sx={{ mt: 4 }}>
          <Typography variant="h5" gutterBottom>Run History</Typography>
          <TableContainer component={Paper} variant="outlined">
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Status</TableCell>
                  <TableCell>Scenario</TableCell>
                  <TableCell>Started</TableCell>
                  <TableCell>Duration</TableCell>
                  <TableCell>Error</TableCell>
                  <TableCell />
                </TableRow>
              </TableHead>
              <TableBody>
                {recentRuns.map((run) => (
                  <TableRow
                    key={run.runId}
                    hover
                    sx={{ cursor: 'pointer' }}
                    onClick={() => navigate(`/chaos-testing/run/${run.runId}`)}
                  >
                    <TableCell>
                      {!run.isCompleted ? (
                        <HourglassEmptyIcon color="info" fontSize="small" />
                      ) : run.isSuccess ? (
                        <CheckCircleIcon color="success" fontSize="small" />
                      ) : (
                        <ErrorIcon color="error" fontSize="small" />
                      )}
                    </TableCell>
                    <TableCell>{run.scenarioName}</TableCell>
                    <TableCell>{new Date(run.startedAt).toLocaleString()}</TableCell>
                    <TableCell>
                      {run.completedAt
                        ? formatRunDuration(run.startedAt, run.completedAt)
                        : 'Running...'}
                    </TableCell>
                    <TableCell sx={{ maxWidth: 300, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      {run.error ?? '—'}
                    </TableCell>
                    <TableCell>
                      <Button size="small" variant="text">View</Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </Box>
      )}

      <Dialog open={selected !== null} onClose={() => !running && setSelected(null)} maxWidth="sm" fullWidth>
        <DialogTitle>Configure: {selected?.name}</DialogTitle>
        <DialogContent>
          {runError && (
            <Alert severity="warning" sx={{ mb: 2 }}>{runError}</Alert>
          )}
          <Stack spacing={2} sx={{ mt: 1 }}>
            {selected?.settings.map((s) => (
              <SettingField
                key={s.key}
                definition={s}
                value={settings[s.key]}
                onChange={(val) => setSettings((prev) => ({ ...prev, [s.key]: val }))}
              />
            ))}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setSelected(null)} disabled={running}>Cancel</Button>
          <Button
            onClick={handleRun}
            variant="contained"
            color="primary"
            disabled={running}
            startIcon={running ? <CircularProgress size={18} /> : <PlayArrowIcon />}
          >
            {running ? 'Starting...' : 'Run Scenario'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}

function SettingField({
  definition,
  value,
  onChange,
}: {
  definition: ScenarioSettingDefinition;
  value: unknown;
  onChange: (val: unknown) => void;
}) {
  const { key, label, description, type, min, max, options } = definition;

  if (type === 'int') {
    const numVal = typeof value === 'number' ? value : Number(value) || 0;
    if (min !== undefined && max !== undefined) {
      return (
        <Box>
          <Typography variant="body2" gutterBottom>{label}: {numVal}</Typography>
          <Slider
            value={numVal}
            min={min}
            max={max}
            step={1}
            onChange={(_, v) => onChange(v as number)}
            valueLabelDisplay="auto"
            size="small"
          />
          <Typography variant="caption" color="text.secondary">{description}</Typography>
        </Box>
      );
    }
    return (
      <TextField
        label={label}
        helperText={description}
        type="number"
        value={numVal}
        onChange={(e) => onChange(parseInt(e.target.value, 10) || 0)}
        size="small"
        fullWidth
      />
    );
  }

  if (type === 'select' && options) {
    return (
      <TextField
        select
        label={label}
        helperText={description}
        value={String(value ?? '')}
        onChange={(e) => onChange(e.target.value)}
        size="small"
        fullWidth
      >
        {options.map((opt) => (
          <MenuItem key={opt} value={opt}>{opt}</MenuItem>
        ))}
      </TextField>
    );
  }

  if (type === 'multiselect' && options) {
    const selected = typeof value === 'string' ? value.split(',').filter(Boolean) : [];
    return (
      <FormControl size="small" fullWidth>
        <InputLabel>{label}</InputLabel>
        <Select
          multiple
          value={selected}
          onChange={(e: SelectChangeEvent<string[]>) => {
            const val = e.target.value;
            onChange(typeof val === 'string' ? val : val.join(','));
          }}
          input={<OutlinedInput label={label} />}
          renderValue={(sel) => (
            <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
              {sel.map((v) => <Chip key={v} label={v} size="small" />)}
            </Box>
          )}
        >
          {options.map((opt) => (
            <MenuItem key={opt} value={opt}>{opt}</MenuItem>
          ))}
        </Select>
        <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, ml: 1.5 }}>{description}</Typography>
      </FormControl>
    );
  }

  return (
    <TextField
      label={label}
      helperText={description}
      value={String(value ?? '')}
      onChange={(e) => onChange(e.target.value)}
      size="small"
      fullWidth
      slotProps={{ htmlInput: { id: key } }}
    />
  );
}

function formatRunDuration(startIso: string, endIso: string): string {
  const ms = new Date(endIso).getTime() - new Date(startIso).getTime();
  const seconds = Math.floor(ms / 1000);
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  const remaining = seconds % 60;
  return `${minutes}m ${remaining}s`;
}

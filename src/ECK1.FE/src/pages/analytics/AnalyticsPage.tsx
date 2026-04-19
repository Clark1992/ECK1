import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  FormControl,
  Grid2 as Grid,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material';
import QueryStatsIcon from '@mui/icons-material/QueryStats';
import DonutLargeIcon from '@mui/icons-material/DonutLarge';
import PublicIcon from '@mui/icons-material/Public';
import TimelineIcon from '@mui/icons-material/Timeline';
import InsightsIcon from '@mui/icons-material/Insights';
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { analyticsApi } from '../../api/analytics';
import type {
  AnalyticsAggregation,
  AnalyticsBreakdownMetric,
  AnalyticsBucket,
  AnalyticsDataset,
  AnalyticsDimension,
  AnalyticsKpiItem,
  AnalyticsTrendMetric,
  AnalyticsTrendResponse,
} from '../../types/analytics';

type RangePreset = '24h' | '7d' | '30d' | '90d';
type BuilderMode = 'trend' | 'breakdown';

type AppliedRange = {
  from: string;
  to: string;
  bucket: AnalyticsBucket;
};

const chartColors = ['#1976d2', '#2e7d32', '#ed6c02', '#9c27b0', '#d32f2f', '#0288d1', '#6d4c41'];

const datasetLabels: Record<AnalyticsDataset, string> = {
  Events: 'Event flow',
  Samples: 'Samples snapshot',
  Orders: 'Orders snapshot',
};

const bucketLabels: Record<AnalyticsBucket, string> = {
  Auto: 'Auto',
  Hour: 'Hour',
  Day: 'Day',
  Week: 'Week',
};

const aggregationLabels: Record<AnalyticsAggregation, string> = {
  Sum: 'Sum',
  Avg: 'Average',
  Min: 'Minimum',
  Max: 'Maximum',
};

const trendMetricLabels: Record<AnalyticsTrendMetric, string> = {
  EventCount: 'Event count',
  UniqueEntities: 'Unique entities',
  SampleCount: 'Sample count',
  AttachmentsCount: 'Attachments',
  OrderCount: 'Order count',
  OrderGrossValue: 'Gross value',
  Units: 'Units',
};

const breakdownMetricLabels: Record<AnalyticsBreakdownMetric, string> = {
  EventCount: 'Event count',
  UniqueEntities: 'Unique entities',
  SampleCount: 'Sample count',
  AttachmentsCount: 'Attachments',
  AttachmentCoverage: 'Attachment coverage',
  OrderCount: 'Order count',
  OrderGrossValue: 'Gross value',
  AvgOrderValue: 'Average order value',
  Units: 'Units',
};

const dimensionLabels: Record<AnalyticsDimension, string> = {
  EntityType: 'Entity type',
  EventType: 'Event type',
  SampleCountry: 'Sample country',
  Status: 'Status',
  CustomerSegment: 'Customer segment',
  ShippingCountry: 'Shipping country',
};

function buildPresetRange(preset: RangePreset): { from: Date; to: Date } {
  const to = new Date();
  const from = new Date(to);

  switch (preset) {
    case '24h':
      from.setHours(to.getHours() - 24);
      break;
    case '7d':
      from.setDate(to.getDate() - 7);
      break;
    case '30d':
      from.setDate(to.getDate() - 30);
      break;
    case '90d':
      from.setDate(to.getDate() - 90);
      break;
  }

  return { from, to };
}

function toDateTimeLocalValue(date: Date): string {
  const pad = (value: number) => String(value).padStart(2, '0');
  return [
    date.getFullYear(),
    '-',
    pad(date.getMonth() + 1),
    '-',
    pad(date.getDate()),
    'T',
    pad(date.getHours()),
    ':',
    pad(date.getMinutes()),
  ].join('');
}

function createAppliedRange(preset: RangePreset, bucket: AnalyticsBucket): AppliedRange {
  const range = buildPresetRange(preset);
  return {
    from: range.from.toISOString(),
    to: range.to.toISOString(),
    bucket,
  };
}

function getDatasetDefaults(dataset: AnalyticsDataset) {
  switch (dataset) {
    case 'Events':
      return {
        trendMetric: 'EventCount' as AnalyticsTrendMetric,
        breakdownMetric: 'EventCount' as AnalyticsBreakdownMetric,
        groupBy: 'EntityType' as AnalyticsDimension,
        dimension: 'EntityType' as AnalyticsDimension,
      };
    case 'Samples':
      return {
        trendMetric: 'SampleCount' as AnalyticsTrendMetric,
        breakdownMetric: 'SampleCount' as AnalyticsBreakdownMetric,
        groupBy: 'SampleCountry' as AnalyticsDimension,
        dimension: 'SampleCountry' as AnalyticsDimension,
      };
    case 'Orders':
      return {
        trendMetric: 'OrderGrossValue' as AnalyticsTrendMetric,
        breakdownMetric: 'OrderCount' as AnalyticsBreakdownMetric,
        groupBy: 'Status' as AnalyticsDimension,
        dimension: 'Status' as AnalyticsDimension,
      };
  }
}

function getTrendMetricOptions(dataset: AnalyticsDataset): AnalyticsTrendMetric[] {
  switch (dataset) {
    case 'Events':
      return ['EventCount', 'UniqueEntities'];
    case 'Samples':
      return ['SampleCount', 'AttachmentsCount'];
    case 'Orders':
      return ['OrderCount', 'OrderGrossValue', 'Units'];
  }
}

function getBreakdownMetricOptions(dataset: AnalyticsDataset): AnalyticsBreakdownMetric[] {
  switch (dataset) {
    case 'Events':
      return ['EventCount', 'UniqueEntities'];
    case 'Samples':
      return ['SampleCount', 'AttachmentsCount', 'AttachmentCoverage'];
    case 'Orders':
      return ['OrderCount', 'OrderGrossValue', 'AvgOrderValue', 'Units'];
  }
}

function getTrendGroupOptions(dataset: AnalyticsDataset): AnalyticsDimension[] {
  switch (dataset) {
    case 'Events':
      return ['EntityType', 'EventType'];
    case 'Samples':
      return ['SampleCountry'];
    case 'Orders':
      return ['Status', 'CustomerSegment', 'ShippingCountry'];
  }
}

function getBreakdownDimensionOptions(dataset: AnalyticsDataset): AnalyticsDimension[] {
  switch (dataset) {
    case 'Events':
      return ['EntityType', 'EventType'];
    case 'Samples':
      return ['SampleCountry'];
    case 'Orders':
      return ['Status', 'CustomerSegment', 'ShippingCountry'];
  }
}

function isTrendAggregationEnabled(metric: AnalyticsTrendMetric): boolean {
  return metric === 'AttachmentsCount' || metric === 'OrderGrossValue' || metric === 'Units';
}

function isBreakdownAggregationEnabled(metric: AnalyticsBreakdownMetric): boolean {
  return metric === 'AttachmentsCount' || metric === 'OrderGrossValue' || metric === 'Units';
}

function formatNumber(value: number, fractionDigits = 0): string {
  return value.toLocaleString(undefined, {
    maximumFractionDigits: fractionDigits,
    minimumFractionDigits: fractionDigits,
  });
}

function formatKpiValue(item: AnalyticsKpiItem): string {
  switch (item.unit) {
    case 'percent':
      return `${formatNumber(item.value, 1)}%`;
    case 'amount':
      return formatNumber(item.value, 2);
    default:
      return formatNumber(item.value, 0);
  }
}

function formatAxisValue(value: number, label: string): string {
  if (label.includes('Coverage')) {
    return `${formatNumber(value, 1)}%`;
  }

  if (label.includes('value')) {
    return formatNumber(value, 2);
  }

  return formatNumber(value, 0);
}

function formatTimeLabel(value: string, bucket: AnalyticsBucket): string {
  const date = new Date(value);
  if (bucket === 'Hour') {
    return date.toLocaleString([], { month: 'short', day: 'numeric', hour: '2-digit' });
  }

  return date.toLocaleDateString([], { month: 'short', day: 'numeric' });
}

function buildTrendChartRows(response?: AnalyticsTrendResponse) {
  const rows = new Map<string, Record<string, string | number>>();
  const series = response?.series ?? [];

  for (const item of series) {
    for (const point of item.points) {
      const existing = rows.get(point.time) ?? { time: point.time };
      existing[item.key] = point.value;
      rows.set(point.time, existing);
    }
  }

  return [...rows.values()].sort((left, right) => {
    const leftTime = String(left.time);
    const rightTime = String(right.time);
    return new Date(leftTime).getTime() - new Date(rightTime).getTime();
  });
}

function renderChartState(loading: boolean, error: Error | null | undefined, isEmpty: boolean) {
  if (loading) {
    return <Typography color="text.secondary">Loading analytics…</Typography>;
  }

  if (error) {
    return <Alert severity="error">{error.message}</Alert>;
  }

  if (isEmpty) {
    return <Typography color="text.secondary">No analytics data for the selected slice.</Typography>;
  }

  return null;
}

export default function AnalyticsPage() {
  const initialRange = buildPresetRange('30d');

  const [selectedPreset, setSelectedPreset] = useState<RangePreset | null>('30d');
  const [draftFrom, setDraftFrom] = useState(toDateTimeLocalValue(initialRange.from));
  const [draftTo, setDraftTo] = useState(toDateTimeLocalValue(initialRange.to));
  const [draftBucket, setDraftBucket] = useState<AnalyticsBucket>('Auto');
  const [appliedRange, setAppliedRange] = useState<AppliedRange>(createAppliedRange('30d', 'Auto'));

  const [builderMode, setBuilderMode] = useState<BuilderMode>('trend');
  const [builderDataset, setBuilderDataset] = useState<AnalyticsDataset>('Events');
  const defaults = getDatasetDefaults('Events');
  const [trendMetric, setTrendMetric] = useState<AnalyticsTrendMetric>(defaults.trendMetric);
  const [breakdownMetric, setBreakdownMetric] = useState<AnalyticsBreakdownMetric>(defaults.breakdownMetric);
  const [groupBy, setGroupBy] = useState<AnalyticsDimension | ''>(defaults.groupBy);
  const [dimension, setDimension] = useState<AnalyticsDimension>(defaults.dimension);
  const [aggregation, setAggregation] = useState<AnalyticsAggregation>('Sum');
  const [top, setTop] = useState(8);
  const [rangeError, setRangeError] = useState('');

  const trendMetricOptions = getTrendMetricOptions(builderDataset);
  const breakdownMetricOptions = getBreakdownMetricOptions(builderDataset);
  const trendGroupOptions = getTrendGroupOptions(builderDataset);
  const breakdownDimensionOptions = getBreakdownDimensionOptions(builderDataset);

  const overviewQuery = useQuery({
    queryKey: ['analytics', 'overview', appliedRange],
    queryFn: () => analyticsApi.overview(appliedRange),
  });

  const eventFlowQuery = useQuery({
    queryKey: ['analytics', 'preset', 'event-flow', appliedRange],
    queryFn: () => analyticsApi.trend({
      ...appliedRange,
      dataset: 'Events',
      metric: 'EventCount',
      bucket: appliedRange.bucket,
      aggregation: 'Sum',
      groupBy: 'EntityType',
    }),
  });

  const orderStatusQuery = useQuery({
    queryKey: ['analytics', 'preset', 'order-status', appliedRange],
    queryFn: () => analyticsApi.breakdown({
      ...appliedRange,
      dataset: 'Orders',
      metric: 'OrderCount',
      dimension: 'Status',
      aggregation: 'Sum',
      top: 8,
    }),
  });

  const segmentValueQuery = useQuery({
    queryKey: ['analytics', 'preset', 'segment-value', appliedRange],
    queryFn: () => analyticsApi.breakdown({
      ...appliedRange,
      dataset: 'Orders',
      metric: 'OrderGrossValue',
      dimension: 'CustomerSegment',
      aggregation: 'Sum',
      top: 6,
    }),
  });

  const sampleCountryQuery = useQuery({
    queryKey: ['analytics', 'preset', 'sample-country', appliedRange],
    queryFn: () => analyticsApi.breakdown({
      ...appliedRange,
      dataset: 'Samples',
      metric: 'SampleCount',
      dimension: 'SampleCountry',
      aggregation: 'Sum',
      top: 6,
    }),
  });

  const builderTrendQuery = useQuery({
    enabled: builderMode === 'trend',
    queryKey: ['analytics', 'builder', 'trend', appliedRange, builderDataset, trendMetric, groupBy, aggregation],
    queryFn: () => analyticsApi.trend({
      ...appliedRange,
      dataset: builderDataset,
      metric: trendMetric,
      bucket: appliedRange.bucket,
      aggregation,
      groupBy: groupBy || undefined,
    }),
  });

  const builderBreakdownQuery = useQuery({
    enabled: builderMode === 'breakdown',
    queryKey: ['analytics', 'builder', 'breakdown', appliedRange, builderDataset, breakdownMetric, dimension, aggregation, top],
    queryFn: () => analyticsApi.breakdown({
      ...appliedRange,
      dataset: builderDataset,
      metric: breakdownMetric,
      dimension,
      aggregation,
      top,
    }),
  });

  const handlePresetChange = (preset: RangePreset) => {
    const range = buildPresetRange(preset);
    setSelectedPreset(preset);
    setDraftFrom(toDateTimeLocalValue(range.from));
    setDraftTo(toDateTimeLocalValue(range.to));
    setAppliedRange({
      from: range.from.toISOString(),
      to: range.to.toISOString(),
      bucket: draftBucket,
    });
    setRangeError('');
  };

  const handleApplyRange = () => {
    const fromDate = new Date(draftFrom);
    const toDate = new Date(draftTo);

    if (Number.isNaN(fromDate.getTime()) || Number.isNaN(toDate.getTime())) {
      setRangeError('Both dates must be valid.');
      return;
    }

    if (fromDate >= toDate) {
      setRangeError('The start of the period must be earlier than the end.');
      return;
    }

    setSelectedPreset(null);
    setAppliedRange({
      from: fromDate.toISOString(),
      to: toDate.toISOString(),
      bucket: draftBucket,
    });
    setRangeError('');
  };

  const handleDatasetChange = (dataset: AnalyticsDataset) => {
    const nextDefaults = getDatasetDefaults(dataset);
    setBuilderDataset(dataset);
    setTrendMetric(nextDefaults.trendMetric);
    setBreakdownMetric(nextDefaults.breakdownMetric);
    setGroupBy(nextDefaults.groupBy);
    setDimension(nextDefaults.dimension);
    setAggregation('Sum');
  };

  const builderTrendData = buildTrendChartRows(builderTrendQuery.data);
  const presetTrendData = buildTrendChartRows(eventFlowQuery.data);
  const builderTrendSeries = builderTrendQuery.data?.series ?? [];
  const presetTrendSeries = eventFlowQuery.data?.series ?? [];

  const builderBreakdownData = builderBreakdownQuery.data?.items ?? [];
  const orderStatusData = orderStatusQuery.data?.items ?? [];
  const segmentValueData = segmentValueQuery.data?.items ?? [];
  const sampleCountryData = sampleCountryQuery.data?.items ?? [];

  const builderChartState = builderMode === 'trend'
    ? renderChartState(builderTrendQuery.isLoading, builderTrendQuery.error, builderTrendData.length === 0)
    : renderChartState(builderBreakdownQuery.isLoading, builderBreakdownQuery.error, builderBreakdownData.length === 0);

  return (
    <Box>
      <Stack direction={{ xs: 'column', lg: 'row' }} justifyContent="space-between" spacing={2} mb={3}>
        <Box>
          <Typography variant="h4" gutterBottom>
            Analytics
          </Typography>
          <Typography color="text.secondary">
            Preset dashboard for event flow and latest business state, plus a limited builder for custom slices.
          </Typography>
        </Box>
        <Chip icon={<InsightsIcon />} label="Snapshot datasets use the latest state of entities updated in the period" color="primary" variant="outlined" />
      </Stack>

      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack direction={{ xs: 'column', lg: 'row' }} spacing={2} alignItems={{ xs: 'stretch', lg: 'center' }}>
          <Stack direction="row" spacing={1} flexWrap="wrap">
            {(['24h', '7d', '30d', '90d'] as RangePreset[]).map((preset) => (
              <Button
                key={preset}
                variant={selectedPreset === preset ? 'contained' : 'outlined'}
                onClick={() => handlePresetChange(preset)}
              >
                {preset}
              </Button>
            ))}
          </Stack>

          <TextField
            label="From"
            type="datetime-local"
            size="small"
            value={draftFrom}
            onChange={(event) => setDraftFrom(event.target.value)}
            InputLabelProps={{ shrink: true }}
            sx={{ minWidth: 220 }}
          />

          <TextField
            label="To"
            type="datetime-local"
            size="small"
            value={draftTo}
            onChange={(event) => setDraftTo(event.target.value)}
            InputLabelProps={{ shrink: true }}
            sx={{ minWidth: 220 }}
          />

          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel>Bucket</InputLabel>
            <Select label="Bucket" value={draftBucket} onChange={(event) => setDraftBucket(event.target.value as AnalyticsBucket)}>
              {(['Auto', 'Hour', 'Day', 'Week'] as AnalyticsBucket[]).map((bucket) => (
                <MenuItem key={bucket} value={bucket}>{bucketLabels[bucket]}</MenuItem>
              ))}
            </Select>
          </FormControl>

          <Button variant="contained" onClick={handleApplyRange} sx={{ alignSelf: { xs: 'stretch', lg: 'center' } }}>
            Apply
          </Button>
        </Stack>

        {rangeError && <Alert severity="error" sx={{ mt: 2 }}>{rangeError}</Alert>}
      </Paper>

      <Grid container spacing={2.5} mb={3}>
        {(overviewQuery.data?.kpis ?? []).map((item) => (
          <Grid key={item.key} size={{ xs: 12, sm: 6, xl: 3 }}>
            <Card sx={{ height: '100%' }}>
              <CardContent>
                <Typography variant="overline" color="text.secondary">{item.label}</Typography>
                <Typography variant="h4" sx={{ mt: 1, mb: 1.5 }}>{formatKpiValue(item)}</Typography>
                <Typography variant="body2" color="text.secondary">{item.hint}</Typography>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>

      {overviewQuery.isLoading && <Alert severity="info" sx={{ mb: 3 }}>Loading overview metrics…</Alert>}
      {overviewQuery.error && <Alert severity="error" sx={{ mb: 3 }}>{overviewQuery.error.message}</Alert>}

      <Grid container spacing={2.5} mb={3}>
        <Grid size={{ xs: 12, xl: 8 }}>
          <Card sx={{ height: '100%' }}>
            <CardContent>
              <Stack direction="row" spacing={1} alignItems="center" mb={2}>
                <TimelineIcon color="primary" />
                <Typography variant="h6">Event flow by entity type</Typography>
              </Stack>
              {renderChartState(eventFlowQuery.isLoading, eventFlowQuery.error, presetTrendData.length === 0) || (
                <ResponsiveContainer width="100%" height={320}>
                  <AreaChart data={presetTrendData}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="time" tickFormatter={(value) => formatTimeLabel(String(value), eventFlowQuery.data?.appliedBucket ?? 'Auto')} minTickGap={24} />
                    <YAxis tickFormatter={(value) => formatAxisValue(Number(value), eventFlowQuery.data?.valueLabel ?? 'Events')} />
                    <Tooltip formatter={(value) => formatAxisValue(Number(value ?? 0), eventFlowQuery.data?.valueLabel ?? 'Events')} labelFormatter={(value) => formatTimeLabel(String(value), eventFlowQuery.data?.appliedBucket ?? 'Auto')} />
                    <Legend />
                    {presetTrendSeries.map((series, index) => (
                      <Area
                        key={series.key}
                        type="monotone"
                        dataKey={series.key}
                        name={series.label}
                        stackId="preset-events"
                        stroke={chartColors[index % chartColors.length]}
                        fill={chartColors[index % chartColors.length]}
                        fillOpacity={0.18}
                      />
                    ))}
                  </AreaChart>
                </ResponsiveContainer>
              )}
            </CardContent>
          </Card>
        </Grid>

        <Grid size={{ xs: 12, md: 6, xl: 4 }}>
          <Card sx={{ height: '100%' }}>
            <CardContent>
              <Stack direction="row" spacing={1} alignItems="center" mb={2}>
                <DonutLargeIcon color="primary" />
                <Typography variant="h6">Order status mix</Typography>
              </Stack>
              {renderChartState(orderStatusQuery.isLoading, orderStatusQuery.error, orderStatusData.length === 0) || (
                <ResponsiveContainer width="100%" height={320}>
                  <PieChart>
                    <Pie data={orderStatusData} dataKey="value" nameKey="label" innerRadius={70} outerRadius={110} paddingAngle={2}>
                      {orderStatusData.map((entry, index) => (
                        <Cell key={entry.key} fill={chartColors[index % chartColors.length]} />
                      ))}
                    </Pie>
                    <Tooltip formatter={(value) => formatNumber(Number(value ?? 0), 0)} />
                    <Legend />
                  </PieChart>
                </ResponsiveContainer>
              )}
            </CardContent>
          </Card>
        </Grid>

        <Grid size={{ xs: 12, md: 6 }}>
          <Card sx={{ height: '100%' }}>
            <CardContent>
              <Stack direction="row" spacing={1} alignItems="center" mb={2}>
                <QueryStatsIcon color="primary" />
                <Typography variant="h6">Gross value by customer segment</Typography>
              </Stack>
              {renderChartState(segmentValueQuery.isLoading, segmentValueQuery.error, segmentValueData.length === 0) || (
                <ResponsiveContainer width="100%" height={300}>
                  <BarChart data={segmentValueData} layout="vertical" margin={{ left: 12 }}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis type="number" tickFormatter={(value) => formatNumber(Number(value), 0)} />
                    <YAxis type="category" dataKey="label" width={120} />
                    <Tooltip formatter={(value) => formatNumber(Number(value ?? 0), 2)} />
                    <Bar dataKey="value" fill="#1976d2" radius={[0, 6, 6, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              )}
            </CardContent>
          </Card>
        </Grid>

        <Grid size={{ xs: 12, md: 6 }}>
          <Card sx={{ height: '100%' }}>
            <CardContent>
              <Stack direction="row" spacing={1} alignItems="center" mb={2}>
                <PublicIcon color="primary" />
                <Typography variant="h6">Samples by country</Typography>
              </Stack>
              {renderChartState(sampleCountryQuery.isLoading, sampleCountryQuery.error, sampleCountryData.length === 0) || (
                <ResponsiveContainer width="100%" height={300}>
                  <BarChart data={sampleCountryData}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="label" />
                    <YAxis tickFormatter={(value) => formatNumber(Number(value), 0)} allowDecimals={false} />
                    <Tooltip formatter={(value) => formatNumber(Number(value ?? 0), 0)} />
                    <Bar dataKey="value" fill="#2e7d32" radius={[6, 6, 0, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              )}
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      <Card>
        <CardContent>
          <Stack direction={{ xs: 'column', lg: 'row' }} justifyContent="space-between" spacing={2} mb={3}>
            <Box>
              <Typography variant="h5" gutterBottom>Chart builder</Typography>
              <Typography color="text.secondary">
                Limited controls by design: only combinations that map to defensible analytics queries are exposed.
              </Typography>
            </Box>
            <ToggleButtonGroup
              exclusive
              value={builderMode}
              onChange={(_, value: BuilderMode | null) => value && setBuilderMode(value)}
              size="small"
            >
              <ToggleButton value="trend">Trend</ToggleButton>
              <ToggleButton value="breakdown">Breakdown</ToggleButton>
            </ToggleButtonGroup>
          </Stack>

          <Grid container spacing={2} mb={3}>
            <Grid size={{ xs: 12, md: 3 }}>
              <FormControl fullWidth size="small">
                <InputLabel>Dataset</InputLabel>
                <Select label="Dataset" value={builderDataset} onChange={(event) => handleDatasetChange(event.target.value as AnalyticsDataset)}>
                  {(['Events', 'Samples', 'Orders'] as AnalyticsDataset[]).map((dataset) => (
                    <MenuItem key={dataset} value={dataset}>{datasetLabels[dataset]}</MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Grid>

            {builderMode === 'trend' ? (
              <>
                <Grid size={{ xs: 12, md: 3 }}>
                  <FormControl fullWidth size="small">
                    <InputLabel>Metric</InputLabel>
                    <Select label="Metric" value={trendMetric} onChange={(event) => setTrendMetric(event.target.value as AnalyticsTrendMetric)}>
                      {trendMetricOptions.map((metric) => (
                        <MenuItem key={metric} value={metric}>{trendMetricLabels[metric]}</MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                </Grid>

                <Grid size={{ xs: 12, md: 3 }}>
                  <FormControl fullWidth size="small">
                    <InputLabel>Split by</InputLabel>
                    <Select label="Split by" value={groupBy} onChange={(event) => setGroupBy(event.target.value as AnalyticsDimension | '')}>
                      <MenuItem value=""><em>No split</em></MenuItem>
                      {trendGroupOptions.map((option) => (
                        <MenuItem key={option} value={option}>{dimensionLabels[option]}</MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                </Grid>
              </>
            ) : (
              <>
                <Grid size={{ xs: 12, md: 3 }}>
                  <FormControl fullWidth size="small">
                    <InputLabel>Metric</InputLabel>
                    <Select label="Metric" value={breakdownMetric} onChange={(event) => setBreakdownMetric(event.target.value as AnalyticsBreakdownMetric)}>
                      {breakdownMetricOptions.map((metric) => (
                        <MenuItem key={metric} value={metric}>{breakdownMetricLabels[metric]}</MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                </Grid>

                <Grid size={{ xs: 12, md: 3 }}>
                  <FormControl fullWidth size="small">
                    <InputLabel>Dimension</InputLabel>
                    <Select label="Dimension" value={dimension} onChange={(event) => setDimension(event.target.value as AnalyticsDimension)}>
                      {breakdownDimensionOptions.map((option) => (
                        <MenuItem key={option} value={option}>{dimensionLabels[option]}</MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                </Grid>
              </>
            )}

            <Grid size={{ xs: 12, md: 2 }}>
              <FormControl fullWidth size="small" disabled={builderMode === 'trend' ? !isTrendAggregationEnabled(trendMetric) : !isBreakdownAggregationEnabled(breakdownMetric)}>
                <InputLabel>Aggregation</InputLabel>
                <Select label="Aggregation" value={aggregation} onChange={(event) => setAggregation(event.target.value as AnalyticsAggregation)}>
                  {(['Sum', 'Avg', 'Min', 'Max'] as AnalyticsAggregation[]).map((option) => (
                    <MenuItem key={option} value={option}>{aggregationLabels[option]}</MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Grid>

            <Grid size={{ xs: 12, md: 1 }}>
              <TextField
                label="Top"
                size="small"
                type="number"
                value={top}
                disabled={builderMode === 'trend'}
                onChange={(event) => setTop(Math.max(1, Math.min(25, Number(event.target.value) || 8)))}
                inputProps={{ min: 1, max: 25 }}
                fullWidth
              />
            </Grid>
          </Grid>

          <Paper variant="outlined" sx={{ p: 2.5, backgroundColor: 'background.default' }}>
            <Typography variant="subtitle1" gutterBottom>
              {builderMode === 'trend'
                ? `${trendMetricLabels[trendMetric]} ${groupBy ? `split by ${dimensionLabels[groupBy]}` : 'trend'}`
                : `${breakdownMetricLabels[breakdownMetric]} by ${dimensionLabels[dimension]}`}
            </Typography>

            {builderChartState || (builderMode === 'trend' ? (
              <ResponsiveContainer width="100%" height={340}>
                <AreaChart data={builderTrendData}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="time" tickFormatter={(value) => formatTimeLabel(String(value), builderTrendQuery.data?.appliedBucket ?? 'Auto')} minTickGap={24} />
                  <YAxis tickFormatter={(value) => formatAxisValue(Number(value), builderTrendQuery.data?.valueLabel ?? trendMetricLabels[trendMetric])} />
                  <Tooltip formatter={(value) => formatAxisValue(Number(value ?? 0), builderTrendQuery.data?.valueLabel ?? trendMetricLabels[trendMetric])} labelFormatter={(value) => formatTimeLabel(String(value), builderTrendQuery.data?.appliedBucket ?? 'Auto')} />
                  <Legend />
                  {builderTrendSeries.map((series, index) => (
                    <Area
                      key={series.key}
                      type="monotone"
                      dataKey={series.key}
                      name={series.label}
                      stroke={chartColors[index % chartColors.length]}
                      fill={chartColors[index % chartColors.length]}
                      fillOpacity={0.18}
                    />
                  ))}
                </AreaChart>
              </ResponsiveContainer>
            ) : (
              <ResponsiveContainer width="100%" height={340}>
                <BarChart data={builderBreakdownData} layout="vertical" margin={{ left: 12 }}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis type="number" tickFormatter={(value) => formatAxisValue(Number(value), builderBreakdownQuery.data?.valueLabel ?? breakdownMetricLabels[breakdownMetric])} />
                  <YAxis type="category" dataKey="label" width={160} />
                  <Tooltip formatter={(value) => formatAxisValue(Number(value ?? 0), builderBreakdownQuery.data?.valueLabel ?? breakdownMetricLabels[breakdownMetric])} />
                  <Bar dataKey="value" radius={[0, 6, 6, 0]}>
                    {builderBreakdownData.map((item, index) => (
                      <Cell key={item.key} fill={chartColors[index % chartColors.length]} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            ))}
          </Paper>
        </CardContent>
      </Card>
    </Box>
  );
}
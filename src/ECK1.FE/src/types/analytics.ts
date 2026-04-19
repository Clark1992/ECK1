export type AnalyticsDataset = 'Events' | 'Samples' | 'Orders';

export type AnalyticsBucket = 'Auto' | 'Hour' | 'Day' | 'Week';

export type AnalyticsAggregation = 'Sum' | 'Avg' | 'Min' | 'Max';

export type AnalyticsTrendMetric =
  | 'EventCount'
  | 'UniqueEntities'
  | 'SampleCount'
  | 'AttachmentsCount'
  | 'OrderCount'
  | 'OrderGrossValue'
  | 'Units';

export type AnalyticsBreakdownMetric =
  | 'EventCount'
  | 'UniqueEntities'
  | 'SampleCount'
  | 'AttachmentsCount'
  | 'AttachmentCoverage'
  | 'OrderCount'
  | 'OrderGrossValue'
  | 'AvgOrderValue'
  | 'Units';

export type AnalyticsDimension =
  | 'EntityType'
  | 'EventType'
  | 'SampleCountry'
  | 'Status'
  | 'CustomerSegment'
  | 'ShippingCountry';

export interface AnalyticsKpiItem {
  key: string;
  label: string;
  value: number;
  unit: string;
  hint: string;
}

export interface AnalyticsOverviewResponse {
  kpis: AnalyticsKpiItem[];
}

export interface AnalyticsTrendPoint {
  time: string;
  value: number;
}

export interface AnalyticsTrendSeries {
  key: string;
  label: string;
  points: AnalyticsTrendPoint[];
}

export interface AnalyticsTrendResponse {
  title: string;
  valueLabel: string;
  appliedBucket: AnalyticsBucket;
  series: AnalyticsTrendSeries[];
}

export interface AnalyticsBreakdownItem {
  key: string;
  label: string;
  value: number;
}

export interface AnalyticsBreakdownResponse {
  title: string;
  valueLabel: string;
  items: AnalyticsBreakdownItem[];
}
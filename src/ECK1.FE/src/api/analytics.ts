import { API, apiFetch, queryString } from './client';
import type {
  AnalyticsAggregation,
  AnalyticsBreakdownMetric,
  AnalyticsBreakdownResponse,
  AnalyticsBucket,
  AnalyticsDataset,
  AnalyticsDimension,
  AnalyticsOverviewResponse,
  AnalyticsTrendMetric,
  AnalyticsTrendResponse,
} from '../types/analytics';

export interface AnalyticsRangeParams {
  from: string;
  to: string;
}

export interface AnalyticsTrendParams extends AnalyticsRangeParams {
  dataset: AnalyticsDataset;
  metric: AnalyticsTrendMetric;
  bucket?: AnalyticsBucket;
  aggregation?: AnalyticsAggregation;
  groupBy?: AnalyticsDimension;
}

export interface AnalyticsBreakdownParams extends AnalyticsRangeParams {
  dataset: AnalyticsDataset;
  metric: AnalyticsBreakdownMetric;
  dimension: AnalyticsDimension;
  aggregation?: AnalyticsAggregation;
  top?: number;
}

export const analyticsApi = {
  overview(params: AnalyticsRangeParams) {
    const qs = queryString({
      From: params.from,
      To: params.to,
    });

    return apiFetch<AnalyticsOverviewResponse>(API.analytics.overview + qs);
  },

  trend(params: AnalyticsTrendParams) {
    const qs = queryString({
      From: params.from,
      To: params.to,
      Dataset: params.dataset,
      Metric: params.metric,
      Bucket: params.bucket,
      Aggregation: params.aggregation,
      GroupBy: params.groupBy,
    });

    return apiFetch<AnalyticsTrendResponse>(API.analytics.trend + qs);
  },

  breakdown(params: AnalyticsBreakdownParams) {
    const qs = queryString({
      From: params.from,
      To: params.to,
      Dataset: params.dataset,
      Metric: params.metric,
      Dimension: params.dimension,
      Aggregation: params.aggregation,
      Top: params.top,
    });

    return apiFetch<AnalyticsBreakdownResponse>(API.analytics.breakdown + qs);
  },
};
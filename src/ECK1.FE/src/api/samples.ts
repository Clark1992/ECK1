import { API, apiFetch, queryString } from './client';
import type { PagedResponse, CommandAccepted, EntityResponse } from '../types/common';
import type { Sample, CreateSampleRequest } from '../types/sample';
import { correlationHeaders } from '../realtime/useRealtimeFeedback';

export interface SampleListParams {
  skip?: number;
  top?: number;
  order?: string;
}

export interface SampleSearchParams extends SampleListParams {
  q?: string;
  hasAttachments?: boolean;
  hasAddress?: boolean;
}

export const samplesApi = {
  list(params: SampleListParams = {}) {
    const qs = queryString({
      Skip: params.skip,
      Top: params.top,
      Order: params.order,
    });
    return apiFetch<PagedResponse<Sample>>(API.samples.list + qs);
  },

  get(id: string) {
    return apiFetch<EntityResponse<Sample>>(API.samples.get(id));
  },

  search(params: SampleSearchParams = {}) {
    const qs = queryString({
      q: params.q,
      HasAttachments: params.hasAttachments,
      HasAddress: params.hasAddress,
      Skip: params.skip,
      Top: params.top,
      Order: params.order,
    });
    return apiFetch<PagedResponse<Sample>>(API.samples.search + qs);
  },

  create(data: CreateSampleRequest, correlationId?: string) {
    const corr = correlationId ? correlationHeaders(correlationId) : undefined;
    return apiFetch<CommandAccepted>(API.samples.create, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', ...corr?.headers },
      body: JSON.stringify(data),
    });
  },

  changeName(id: string, newName: string, expectedVersion: number, correlationId?: string) {
    const corr = correlationId ? correlationHeaders(correlationId) : undefined;
    return apiFetch<CommandAccepted>(API.samples.changeName(id), {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', ...corr?.headers },
      body: JSON.stringify({ newName, expectedVersion }),
    });
  },

  changeDescription(id: string, newDescription: string, expectedVersion: number, correlationId?: string) {
    const corr = correlationId ? correlationHeaders(correlationId) : undefined;
    return apiFetch<CommandAccepted>(API.samples.changeDescription(id), {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', ...corr?.headers },
      body: JSON.stringify({ newDescription, expectedVersion }),
    });
  },
};

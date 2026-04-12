import { API, apiFetch, jsonBody, queryString } from './client';
import type { PagedResponse, CommandAccepted } from '../types/common';
import type { Sample, CreateSampleRequest } from '../types/sample';

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
    return apiFetch<Sample>(API.samples.get(id));
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

  create(data: CreateSampleRequest) {
    return apiFetch<CommandAccepted>(API.samples.create, {
      method: 'POST',
      ...jsonBody(data),
    });
  },

  changeName(id: string, newName: string) {
    return apiFetch<CommandAccepted>(API.samples.changeName(id), {
      method: 'PUT',
      ...jsonBody({ newName }),
    });
  },

  changeDescription(id: string, newDescription: string) {
    return apiFetch<CommandAccepted>(API.samples.changeDescription(id), {
      method: 'PUT',
      ...jsonBody({ newDescription }),
    });
  },
};

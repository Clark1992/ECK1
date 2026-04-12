import { API, apiFetch, jsonBody, queryString } from './client';
import type { PagedResponse, CommandAccepted, Address, Money } from '../types/common';
import type { Sample2, CreateSample2Request, ChangeStatusRequest } from '../types/sample2';

export interface Sample2ListParams {
  skip?: number;
  top?: number;
  order?: string;
}

export interface Sample2SearchParams extends Sample2ListParams {
  q?: string;
  hasCustomer?: boolean;
  hasShippingAddress?: boolean;
  hasLineItems?: boolean;
  tags?: string;
  statuses?: string;
}

export const sample2sApi = {
  list(params: Sample2ListParams = {}) {
    const qs = queryString({
      Skip: params.skip,
      Top: params.top,
      Order: params.order,
    });
    return apiFetch<PagedResponse<Sample2>>(API.sample2s.list + qs);
  },

  get(id: string) {
    return apiFetch<Sample2>(API.sample2s.get(id));
  },

  search(params: Sample2SearchParams = {}) {
    const qs = queryString({
      q: params.q,
      HasCustomer: params.hasCustomer,
      HasShippingAddress: params.hasShippingAddress,
      HasLineItems: params.hasLineItems,
      Tags: params.tags,
      Statuses: params.statuses,
      Skip: params.skip,
      Top: params.top,
      Order: params.order,
    });
    return apiFetch<PagedResponse<Sample2>>(API.sample2s.search + qs);
  },

  create(data: CreateSample2Request) {
    return apiFetch<CommandAccepted>(API.sample2s.create, {
      method: 'POST',
      ...jsonBody(data),
    });
  },

  changeCustomerEmail(id: string, newEmail: string) {
    return apiFetch<CommandAccepted>(API.sample2s.changeCustomerEmail(id), {
      method: 'PUT',
      ...jsonBody({ newEmail }),
    });
  },

  changeShippingAddress(id: string, newAddress: Address) {
    return apiFetch<CommandAccepted>(API.sample2s.changeShippingAddress(id), {
      method: 'PUT',
      ...jsonBody({ newAddress }),
    });
  },

  addLineItem(id: string, item: { sku: string; quantity: number; unitPrice: Money }) {
    return apiFetch<CommandAccepted>(API.sample2s.addLineItem(id), {
      method: 'POST',
      ...jsonBody({ item }),
    });
  },

  removeLineItem(id: string, itemId: string) {
    return apiFetch<CommandAccepted>(API.sample2s.removeLineItem(id, itemId), {
      method: 'DELETE',
    });
  },

  changeStatus(id: string, data: ChangeStatusRequest) {
    return apiFetch<CommandAccepted>(API.sample2s.changeStatus(id), {
      method: 'PUT',
      ...jsonBody(data),
    });
  },

  addTag(id: string, tag: string) {
    const qs = queryString({ tag });
    return apiFetch<CommandAccepted>(API.sample2s.addTag(id) + qs, {
      method: 'POST',
    });
  },

  removeTag(id: string, tag: string) {
    const qs = queryString({ tag });
    return apiFetch<CommandAccepted>(API.sample2s.removeTag(id) + qs, {
      method: 'DELETE',
    });
  },
};

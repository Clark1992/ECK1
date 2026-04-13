import { API, apiFetch, jsonBody, queryString } from './client';
import type { PagedResponse, CommandAccepted, Address, Money, EntityResponse } from '../types/common';
import type { Sample2, CreateSample2Request, ChangeStatusRequest } from '../types/sample2';
import { correlationHeaders } from '../realtime/useRealtimeFeedback';

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
    return apiFetch<EntityResponse<Sample2>>(API.sample2s.get(id));
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

  create(data: CreateSample2Request, correlationId?: string) {
    const corr = correlationId ? correlationHeaders(correlationId) : undefined;
    return apiFetch<CommandAccepted>(API.sample2s.create, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', ...corr?.headers },
      body: JSON.stringify(data),
    });
  },

  changeCustomerEmail(id: string, newEmail: string, expectedVersion: number, correlationId?: string) {
    const corr = correlationId ? correlationHeaders(correlationId) : undefined;
    return apiFetch<CommandAccepted>(API.sample2s.changeCustomerEmail(id), {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', ...corr?.headers },
      body: JSON.stringify({ newEmail, expectedVersion }),
    });
  },

  changeShippingAddress(id: string, newAddress: Address, expectedVersion: number, correlationId?: string) {
    const corr = correlationId ? correlationHeaders(correlationId) : undefined;
    return apiFetch<CommandAccepted>(API.sample2s.changeShippingAddress(id), {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', ...corr?.headers },
      body: JSON.stringify({ newAddress, expectedVersion }),
    });
  },

  addLineItem(id: string, item: { sku: string; quantity: number; unitPrice: Money }, expectedVersion: number, correlationId?: string) {
    const corr = correlationId ? correlationHeaders(correlationId) : undefined;
    return apiFetch<CommandAccepted>(API.sample2s.addLineItem(id), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', ...corr?.headers },
      body: JSON.stringify({ item, expectedVersion }),
    });
  },

  removeLineItem(id: string, itemId: string, correlationId?: string) {
    const corr = correlationId ? correlationHeaders(correlationId) : undefined;
    return apiFetch<CommandAccepted>(API.sample2s.removeLineItem(id, itemId), {
      method: 'DELETE',
      headers: { ...corr?.headers },
    });
  },

  changeStatus(id: string, data: ChangeStatusRequest, expectedVersion: number, correlationId?: string) {
    const corr = correlationId ? correlationHeaders(correlationId) : undefined;
    return apiFetch<CommandAccepted>(API.sample2s.changeStatus(id), {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', ...corr?.headers },
      body: JSON.stringify({ ...data, expectedVersion }),
    });
  },

  addTag(id: string, tag: string, expectedVersion: number, correlationId?: string) {
    const qs = queryString({ tag });
    const corr = correlationId ? correlationHeaders(correlationId) : undefined;
    return apiFetch<CommandAccepted>(API.sample2s.addTag(id) + qs, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', ...corr?.headers },
      body: JSON.stringify({ expectedVersion }),
    });
  },

  removeTag(id: string, tag: string, correlationId?: string) {
    const qs = queryString({ tag });
    const corr = correlationId ? correlationHeaders(correlationId) : undefined;
    return apiFetch<CommandAccepted>(API.sample2s.removeTag(id) + qs, {
      method: 'DELETE',
      headers: { ...corr?.headers },
    });
  },
};

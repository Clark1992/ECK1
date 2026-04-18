import { API, apiFetch } from './client';
import type { EntityHistoryResponse } from '../types/history';

export const historyApi = {
  getSampleHistory(id: string) {
    return apiFetch<EntityHistoryResponse>(API.history.samples(id));
  },

  getSample2History(id: string) {
    return apiFetch<EntityHistoryResponse>(API.history.sample2s(id));
  },
};

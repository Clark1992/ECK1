import type { User } from 'oidc-client-ts';

const BASE = '/api';
const COMMANDS = `${BASE}/eck1-commandsapi`;
const QUERIES = `${BASE}/eck1-queriesapi`;

export const API = {
  commands: COMMANDS,
  queries: QUERIES,
  samples: {
    list: `${QUERIES}/api/samples`,
    get: (id: string) => `${QUERIES}/api/samples/${id}`,
    search: `${QUERIES}/search/samples`,
    create: `${COMMANDS}/api/async/sample`,
    changeName: (id: string) => `${COMMANDS}/api/async/sample/${id}/name`,
    changeDescription: (id: string) => `${COMMANDS}/api/async/sample/${id}/description`,
  },
  sample2s: {
    list: `${QUERIES}/api/sample2s`,
    get: (id: string) => `${QUERIES}/api/sample2s/${id}`,
    search: `${QUERIES}/search/sample2s`,
    create: `${COMMANDS}/api/async/sample2`,
    changeCustomerEmail: (id: string) => `${COMMANDS}/api/async/sample2/${id}/customer-email`,
    changeShippingAddress: (id: string) => `${COMMANDS}/api/async/sample2/${id}/shipping-address`,
    addLineItem: (id: string) => `${COMMANDS}/api/async/sample2/${id}/line-items`,
    removeLineItem: (id: string, itemId: string) => `${COMMANDS}/api/async/sample2/${id}/line-items/${itemId}`,
    changeStatus: (id: string) => `${COMMANDS}/api/async/sample2/${id}/status`,
    addTag: (id: string) => `${COMMANDS}/api/async/sample2/${id}/tags`,
    removeTag: (id: string) => `${COMMANDS}/api/async/sample2/${id}/tags`,
  },
  history: {
    samples: (id: string) => `${QUERIES}/api/history/samples/${id}`,
    sample2s: (id: string) => `${QUERIES}/api/history/sample2s/${id}`,
  },
} as const;

let _getUser: (() => User | null | undefined) | null = null;

export function setUserAccessor(fn: () => User | null | undefined) {
  _getUser = fn;
}

function authHeaders(): Record<string, string> {
  const user = _getUser?.();
  if (user?.access_token) {
    return { Authorization: `Bearer ${user.access_token}` };
  }
  return {};
}

export async function apiFetch<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, {
    ...init,
    headers: {
      ...authHeaders(),
      ...init?.headers,
    },
  });
  if (!res.ok) {
    const text = await res.text().catch(() => '');
    throw new ApiError(res.status, text || res.statusText);
  }
  if (res.status === 204 || res.headers.get('content-length') === '0') {
    return undefined as T;
  }
  return res.json() as Promise<T>;
}

export class ApiError extends Error {
  constructor(
    public status: number,
    public body: string,
  ) {
    super(`HTTP ${status}: ${body}`);
    this.name = 'ApiError';
  }
}

export function jsonBody(data: unknown): RequestInit {
  return {
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data),
  };
}

export function queryString(params: Record<string, string | number | boolean | undefined>): string {
  const entries = Object.entries(params).filter(
    (entry): entry is [string, string | number | boolean] => entry[1] !== undefined,
  );
  if (entries.length === 0) return '';
  return '?' + new URLSearchParams(entries.map(([k, v]) => [k, String(v)])).toString();
}

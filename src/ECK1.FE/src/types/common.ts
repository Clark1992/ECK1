export interface Address {
  street: string;
  city: string;
  country: string;
}

export interface Money {
  amount: number;
  currency: string;
}

export interface PagedResponse<T> {
  items: T[];
  total: number;
}

export interface CommandAccepted {
  status: string;
  command: string;
  topic: string;
  key: string;
  correlationId: string;
}

export interface EntityResponse<T> {
  data: T;
  isRebuilding: boolean;
}

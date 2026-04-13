import type { Address, Money } from './common';

export enum Sample2Status {
  Draft = 0,
  Submitted = 1,
  Paid = 2,
  Shipped = 3,
  Cancelled = 4,
}

export const Sample2StatusLabels: Record<Sample2Status, string> = {
  [Sample2Status.Draft]: 'Draft',
  [Sample2Status.Submitted]: 'Submitted',
  [Sample2Status.Paid]: 'Paid',
  [Sample2Status.Shipped]: 'Shipped',
  [Sample2Status.Cancelled]: 'Cancelled',
};

export interface Sample2Customer {
  customerId: string;
  email: string;
  segment: string;
}

export interface Sample2Address {
  street: string;
  city: string;
  country: string;
}

export interface Sample2LineItem {
  itemId: string;
  sku: string;
  quantity: number;
  unitPrice: Money;
}

export interface Sample2Tag {
  value: string;
}

export interface Sample2 {
  sample2Id: string;
  version: number;
  customer: Sample2Customer;
  shippingAddress: Sample2Address;
  lineItems: Sample2LineItem[];
  tags: Sample2Tag[];
  status: Sample2Status;
  lastModified: string | null;
}

export interface CreateSample2Request {
  customer: { email: string; segment: string };
  shippingAddress: Address;
  lineItems: { sku: string; quantity: number; unitPrice: Money }[];
  tags: string[];
}

export interface ChangeStatusRequest {
  newStatus: Sample2Status;
  reason: string;
}

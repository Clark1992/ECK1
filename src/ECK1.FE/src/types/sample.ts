import type { Address } from './common';

export interface SampleAttachment {
  id: string;
  fileName: string;
  url: string;
}

export interface SampleAddress {
  street: string;
  city: string;
  country: string;
}

export interface Sample {
  sampleId: string;
  version: number;
  name: string;
  description: string;
  address: SampleAddress | null;
  attachments: SampleAttachment[];
  lastModified: string | null;
}

export interface CreateSampleRequest {
  name: string;
  description: string;
  address: Address | null;
}

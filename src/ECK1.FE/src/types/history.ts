export interface EntityHistoryEvent {
  eventId: string;
  eventType: string;
  occurredAt: string;
  entityVersion: number;
  payload: string;
}

export interface EntityHistoryResponse {
  events: EntityHistoryEvent[];
}

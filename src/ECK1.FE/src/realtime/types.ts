export interface RealtimeFeedbackEvent {
  correlationId: string;
  userId: string;
  eventType: string;
  entityType: string;
  entityId: string;
  success: boolean;
  outcomeCode: string;
  title: string;
  message: string;
  payload?: string;
  version: number;
  timestamp: string;
}

export interface RealtimeFeedbackOptions {
  correlationId: string;
  timeoutMs?: number;
  keepAlive?: boolean;
  onFeedback?: (event: RealtimeFeedbackEvent) => void;
  onTimeout?: () => void;
}

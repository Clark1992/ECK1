import { useEffect, useRef } from 'react';
import {
  getConnection,
  subscribeToEntity,
  unsubscribeFromEntity,
} from './signalrClient';
import type { RealtimeFeedbackEvent } from './types';

export interface UseEntityEventsOptions {
  entityType: string;
  entityId: string | undefined;
  onEvent?: (event: RealtimeFeedbackEvent) => void;
}

/**
 * Subscribe to entity-level realtime events via SignalR.
 * Any viewer of the entity detail page should use this to receive
 * updates when the entity changes (from any user/source).
 */
export function useEntityEvents({ entityType, entityId, onEvent }: UseEntityEventsOptions) {
  const onEventRef = useRef(onEvent);
  onEventRef.current = onEvent;

  useEffect(() => {
    if (!entityType || !entityId) return;

    const handler = (event: RealtimeFeedbackEvent) => {
      if (event.entityType !== entityType || event.entityId !== entityId) return;
      onEventRef.current?.(event);
    };

    const conn = getConnection();
    conn?.on('ReceiveFeedback', handler);
    subscribeToEntity(entityType, entityId).catch(() => {});

    return () => {
      conn?.off('ReceiveFeedback', handler);
      unsubscribeFromEntity(entityType, entityId).catch(() => {});
    };
  }, [entityType, entityId]);
}

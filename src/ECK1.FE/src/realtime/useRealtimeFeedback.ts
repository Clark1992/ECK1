import { useCallback, useEffect, useRef, useState } from 'react';
import {
  addConnectionListener,
  getConnection,
  subscribeToCorrelation,
  unsubscribeFromCorrelation,
} from './signalrClient';
import type { RealtimeFeedbackEvent, RealtimeFeedbackOptions } from './types';
import type { QueryClient } from '@tanstack/react-query';
import type { ShowNotification } from '../notifications/NotificationProvider';

const CORRELATION_HEADER = 'X-Realtime-Correlation-Id';

export function generateCorrelationId(): string {
  return crypto.randomUUID();
}

export function correlationHeaders(correlationId?: string): {
  correlationId: string;
  headers: Record<string, string>;
} {
  const id = correlationId ?? generateCorrelationId();
  return {
    correlationId: id,
    headers: { [CORRELATION_HEADER]: id },
  };
}

/**
 * Low-level hook that subscribes to realtime feedback for a correlationId.
 * When keepAlive is true, the subscription stays open after the first event.
 */
export function useRealtimeFeedback(options: RealtimeFeedbackOptions | null) {
  const { correlationId, timeoutMs = 30_000, keepAlive = false, onFeedback, onTimeout } = options ?? {};
  const cleanedUp = useRef(false);
  const onFeedbackRef = useRef(onFeedback);
  const onTimeoutRef = useRef(onTimeout);
  onFeedbackRef.current = onFeedback;
  onTimeoutRef.current = onTimeout;

  const [connReady, setConnReady] = useState(0);
  useEffect(() => addConnectionListener(() => setConnReady((v) => v + 1)), []);

  useEffect(() => {
    if (!correlationId) return;

    cleanedUp.current = false;
    let timeoutHandle: ReturnType<typeof setTimeout> | undefined;

    const handler = (event: RealtimeFeedbackEvent) => {
      if (event.correlationId !== correlationId) return;
      if (cleanedUp.current) return;

      if (!keepAlive) {
        clearTimeout(timeoutHandle);
        cleanup();
      }
      onFeedbackRef.current?.(event);
    };

    const cleanup = () => {
      if (cleanedUp.current) return;
      cleanedUp.current = true;
      clearTimeout(timeoutHandle);
      const conn = getConnection();
      conn?.off('ReceiveFeedback', handler);
      unsubscribeFromCorrelation(correlationId).catch(() => {});
    };

    const conn = getConnection();
    conn?.on('ReceiveFeedback', handler);
    subscribeToCorrelation(correlationId).catch((err) => {
      console.warn('[Realtime] Failed to subscribe to correlation:', err);
    });

    if (timeoutMs > 0) {
      timeoutHandle = setTimeout(() => {
        if (cleanedUp.current) return;
        cleanup();
        onTimeoutRef.current?.();
      }, timeoutMs);
    }

    return cleanup;
  }, [correlationId, timeoutMs, keepAlive, connReady]);
}

export function useCorrelatedCommand() {
  const [pendingCorrelation, setPendingCorrelation] = useState<string | null>(null);

  const startCorrelatedCall = useCallback(() => {
    const { correlationId, headers } = correlationHeaders();
    setPendingCorrelation(correlationId);
    return { correlationId, headers };
  }, []);

  const clearCorrelation = useCallback(() => {
    setPendingCorrelation(null);
  }, []);

  return { pendingCorrelation, startCorrelatedCall, clearCorrelation };
}

/**
 * Create-entity flow (two-stage via correlation):
 * 1. CommandsAPI feedback (outcomeCode=OK) → transient "creating" banner
 * 2. Mongo plugin feedback (outcomeCode=read.view.created) → notification with link
 */
export function useCreateEntityFeedback(
  queryClient: QueryClient,
  queryKeyPrefix: string,
  basePath: string,
  showNotification: ShowNotification,
  onError?: (event?: RealtimeFeedbackEvent) => void,
) {
  const { pendingCorrelation, startCorrelatedCall, clearCorrelation } = useCorrelatedCommand();
  const originPathRef = useRef<string>('');

  useRealtimeFeedback(
    pendingCorrelation
      ? {
          correlationId: pendingCorrelation,
          timeoutMs: 30_000,
          keepAlive: true,
          onFeedback: (event) => {
            if (!event.success) {
              clearCorrelation();
              showNotification({
                title: event.title || 'Error',
                message: event.message || event.outcomeCode || 'Operation failed',
                severity: 'error',
              });
              onError?.(event);
              return;
            }

            if (event.outcomeCode === 'read.view.created') {
              // Stage 2: Mongo plugin — view is ready
              clearCorrelation();
              queryClient.invalidateQueries({ queryKey: [queryKeyPrefix] });
              showNotification({
                title: event.title || 'Created',
                message: event.message || 'Entity is ready',
                severity: 'success',
                duration: 10_000,
                action: { label: 'View', href: `${basePath}/${event.entityId}` },
              });
            } else {
              // Stage 1: CommandsAPI — command accepted
              showNotification({
                title: event.title || 'Creating...',
                message: event.message || 'Entity is being created',
                severity: 'info',
              });
            }
          },
          onTimeout: () => {
            clearCorrelation();
            onError?.();
          },
        }
      : null,
  );

  const wrappedStart = useCallback(() => {
    originPathRef.current = window.location.pathname;
    return startCorrelatedCall();
  }, [startCorrelatedCall]);

  return { startCorrelatedCall: wrappedStart, clearCorrelation };
}

/**
 * Edit-entity flow (correlation + version memo):
 * 1. CommandsAPI feedback → memorize actual version, show "updating" notification
 * 2. Entity events (via useEntityEvents in the page) check pendingVersion → refetch
 */
export function useEntityUpdateFeedback(
  showNotification: ShowNotification,
  onError?: (event?: RealtimeFeedbackEvent) => void,
) {
  const { pendingCorrelation, startCorrelatedCall, clearCorrelation } = useCorrelatedCommand();
  const [pendingVersion, setPendingVersion] = useState<number | null>(null);
  const pendingVersionRef = useRef<number | null>(null);

  useRealtimeFeedback(
    pendingCorrelation
      ? {
          correlationId: pendingCorrelation,
          timeoutMs: 30_000,
          onFeedback: (event) => {
            clearCorrelation();
            if (!event.success) {
              showNotification({
                title: event.title || 'Error',
                message: event.message || event.outcomeCode || 'Operation failed',
                severity: 'error',
              });
              onError?.(event);
              return;
            }
            setPendingVersion(event.version);
            pendingVersionRef.current = event.version;
          },
          onTimeout: () => {
            clearCorrelation();
            onError?.();
          },
        }
      : null,
  );

  const clearPendingVersion = useCallback(() => {
    setPendingVersion(null);
    pendingVersionRef.current = null;
  }, []);
  const isPending = pendingCorrelation !== null || pendingVersion !== null;

  return { startCorrelatedCall, clearCorrelation, pendingVersion, pendingVersionRef, clearPendingVersion, isPending };
}

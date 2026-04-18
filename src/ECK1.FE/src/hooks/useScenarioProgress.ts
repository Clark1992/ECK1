import { useEffect, useRef, useState } from 'react';
import {
  HubConnectionBuilder,
  HubConnection,
  HubConnectionState,
  LogLevel,
  HttpTransportType,
} from '@microsoft/signalr';
import type { ScenarioProgress } from '../api/testplatform';

const TESTPLATFORM_HUB = '/testplatform/hubs/scenarios';

export function useScenarioProgress(runId: string | null) {
  const [progress, setProgress] = useState<ScenarioProgress | null>(null);
  const [connected, setConnected] = useState(false);
  const connRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    if (!runId) return;

    const connection = new HubConnectionBuilder()
      .withUrl(TESTPLATFORM_HUB, {
        transport: HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents,
        withCredentials: false,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(LogLevel.Information)
      .build();

    connRef.current = connection;

    connection.on('ScenarioProgress', (data: ScenarioProgress) => {
      setProgress(data);
    });

    connection.onreconnected(async () => {
      await connection.invoke('SubscribeToRun', runId).catch(() => {});
    });

    const start = async () => {
      try {
        await connection.start();
        setConnected(true);
        await connection.invoke('SubscribeToRun', runId);
      } catch (err) {
        console.error('[ScenarioHub] Failed to connect:', err);
      }
    };

    start();

    return () => {
      if (connection.state === HubConnectionState.Connected) {
        connection.invoke('UnsubscribeFromRun', runId).catch(() => {});
      }
      connection.stop();
      connRef.current = null;
      setConnected(false);
    };
  }, [runId]);

  return { progress, connected };
}

import {
  HubConnectionBuilder,
  HubConnection,
  LogLevel,
  HubConnectionState,
  HttpTransportType,
} from '@microsoft/signalr';
import type { User } from 'oidc-client-ts';

let connection: HubConnection | null = null;
let userAccessor: (() => User | null | undefined) | null = null;
const activeSubscriptions = {
  correlations: new Set<string>(),
  entities: new Set<string>(),
};

type ConnectionListener = () => void;
const connectionListeners = new Set<ConnectionListener>();

export function addConnectionListener(listener: ConnectionListener): () => void {
  connectionListeners.add(listener);
  return () => { connectionListeners.delete(listener); };
}

export function setRealtimeUserAccessor(fn: () => User | null | undefined) {
  userAccessor = fn;
}

export function getConnection(): HubConnection | null {
  return connection;
}

export async function startConnection(): Promise<HubConnection> {
  if (connection?.state === HubConnectionState.Connected) {
    return connection;
  }

  if (connection) {
    try {
      await connection.stop();
    } catch {
      // ignore
    }
  }

  const hubUrl = '/api/hubs/realtime';

  connection = new HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory: () => {
        const user = userAccessor?.();
        return user?.access_token ?? '';
      },
      transport: HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents,
      withCredentials: false,
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(LogLevel.Information)
    .build();

  connection.onclose((error) => {
    if (error) {
      console.warn('[Realtime] Connection closed with error:', error);
    }
  });

  connection.onreconnected(async () => {
    console.log('[Realtime] Reconnected — re-subscribing');
    for (const cid of activeSubscriptions.correlations) {
      await connection!.invoke('SubscribeToCorrelation', cid).catch(() => {});
    }
    for (const key of activeSubscriptions.entities) {
      const [entityType, entityId] = key.split('|');
      await connection!.invoke('SubscribeToEntity', entityType, entityId).catch(() => {});
    }
  });

  await connection.start();
  console.log('[Realtime] Connected');
  for (const listener of connectionListeners) listener();
  return connection;
}

export async function stopConnection(): Promise<void> {
  if (connection) {
    await connection.stop();
    connection = null;
  }
}

export async function subscribeToCorrelation(correlationId: string): Promise<void> {
  activeSubscriptions.correlations.add(correlationId);
  const conn = getConnection();
  if (conn?.state === HubConnectionState.Connected) {
    await conn.invoke('SubscribeToCorrelation', correlationId);
  }
}

export async function unsubscribeFromCorrelation(correlationId: string): Promise<void> {
  activeSubscriptions.correlations.delete(correlationId);
  const conn = getConnection();
  if (conn?.state === HubConnectionState.Connected) {
    await conn.invoke('UnsubscribeFromCorrelation', correlationId);
  }
}

export async function subscribeToEntity(entityType: string, entityId: string): Promise<void> {
  activeSubscriptions.entities.add(`${entityType}|${entityId}`);
  const conn = getConnection();
  if (conn?.state === HubConnectionState.Connected) {
    await conn.invoke('SubscribeToEntity', entityType, entityId);
  }
}

export async function unsubscribeFromEntity(entityType: string, entityId: string): Promise<void> {
  activeSubscriptions.entities.delete(`${entityType}|${entityId}`);
  const conn = getConnection();
  if (conn?.state === HubConnectionState.Connected) {
    await conn.invoke('UnsubscribeFromEntity', entityType, entityId);
  }
}

import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { TEST_CONFIG } from '../test-config';

export interface SignalRConnectOptions {
  accessToken: string;
  hubUrl: string;
  baseUrl?: string;
}

/**
 * Build a SignalR connection to the given hub. Caller is responsible for `start()` and `stop()`.
 */
export function buildHubConnection(opts: SignalRConnectOptions): HubConnection {
  const baseUrl = opts.baseUrl ?? TEST_CONFIG.appUrl;
  return new HubConnectionBuilder()
    .withUrl(`${baseUrl}${opts.hubUrl}`, {
      accessTokenFactory: () => opts.accessToken,
    })
    .configureLogging(LogLevel.Warning)
    .build();
}

/**
 * Subscribes to a SignalR client method. The .NET hub publishes events with
 * PascalCase names (`EventsReceived`), but the @microsoft/signalr Node client
 * matches them after lowercasing on the wire — so we register both casings
 * defensively so neither side has to know which is being used.
 */
export async function waitForEvent<T = unknown>(
  connection: HubConnection,
  eventName: string,
  predicate: (payload: T) => boolean = () => true,
  timeoutMs = 30_000,
): Promise<T> {
  const lowered = eventName.toLowerCase();
  return new Promise<T>((resolve, reject) => {
    const timeout = setTimeout(() => {
      connection.off(eventName, handler);
      if (lowered !== eventName) {
        connection.off(lowered, handler);
      }
      reject(new Error(`Timed out waiting for SignalR event "${eventName}"`));
    }, timeoutMs);

    const handler = (payload: T) => {
      if (predicate(payload)) {
        clearTimeout(timeout);
        connection.off(eventName, handler);
        if (lowered !== eventName) {
          connection.off(lowered, handler);
        }
        resolve(payload);
      }
    };

    connection.on(eventName, handler);
    if (lowered !== eventName) {
      connection.on(lowered, handler);
    }
  });
}

/**
 * Polls `GetRecentEvents` over SignalR until an event matching the given item hash and
 * delete reason shows up, or the timeout elapses. Matching is case-insensitive on the hash.
 */
export async function findEventWithDeleteReason(
  token: string,
  itemHash: string,
  deleteReason: string,
  timeoutMs: number,
): Promise<Record<string, unknown> | undefined> {
  const connection = buildHubConnection({ accessToken: token, hubUrl: '/api/hubs/app' });
  await connection.start();

  try {
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
      const received = waitForEvent<Array<Record<string, unknown>>>(connection, 'EventsReceived');
      await connection.invoke('GetRecentEvents', 20);
      const events = await received;
      const match = events.find(
        (e) => String(e.itemHash).toLowerCase() === itemHash.toLowerCase() && e.deleteReason === deleteReason,
      );
      if (match) {
        return match;
      }
      await new Promise((r) => setTimeout(r, 500));
    }
    return undefined;
  } finally {
    await connection.stop();
  }
}

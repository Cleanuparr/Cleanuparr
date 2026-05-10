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

export async function waitForEvent<T = unknown>(
  connection: HubConnection,
  eventName: string,
  predicate: (payload: T) => boolean = () => true,
  timeoutMs = 30_000,
): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const timeout = setTimeout(() => {
      connection.off(eventName, handler);
      reject(new Error(`Timed out waiting for SignalR event "${eventName}"`));
    }, timeoutMs);

    const handler = (payload: T) => {
      if (predicate(payload)) {
        clearTimeout(timeout);
        connection.off(eventName, handler);
        resolve(payload);
      }
    };

    connection.on(eventName, handler);
  });
}

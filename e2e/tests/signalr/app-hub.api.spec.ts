import { test, expect } from '../fixtures/base';
import { buildHubConnection, waitForEvent } from '../helpers/api/signalr';
import { adminTokens } from '../helpers/test-lifecycle';

test.describe('SignalR — app hub', () => {
  test('client can connect with bearer token and receive initial state', async () => {
    const tokens = adminTokens();
    const connection = buildHubConnection({
      accessToken: tokens.accessToken,
      hubUrl: '/api/hubs/app',
    });
    await connection.start();
    expect(connection.state).toBe('Connected');

    try {
      const eventsPromise = waitForEvent<unknown[]>(connection, 'EventsReceived');
      await connection.invoke('GetRecentEvents', 5);
      const events = await eventsPromise;
      expect(Array.isArray(events)).toBe(true);

      const strikesPromise = waitForEvent<unknown[]>(connection, 'StrikesReceived');
      await connection.invoke('GetRecentStrikes', 5);
      const strikes = await strikesPromise;
      expect(Array.isArray(strikes)).toBe(true);
    } finally {
      await connection.stop();
    }
  });

  test('connection without token is rejected', async () => {
    const connection = buildHubConnection({
      accessToken: '',
      hubUrl: '/api/hubs/app',
    });
    await expect(connection.start()).rejects.toThrow();
  });
});

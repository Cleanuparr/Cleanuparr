import { test, expect } from '../fixtures/base';
import { buildHubConnection } from '../helpers/api/signalr';

test.describe('SignalR — app hub', () => {
  test('client can connect with bearer token and receive initial state', async ({ workerAdminTokens }) => {
    const connection = buildHubConnection({
      accessToken: workerAdminTokens.accessToken,
      hubUrl: '/api/hubs/app',
    });
    await connection.start();

    expect(connection.state).toBe('Connected');

    try {
      const recentLogs = await connection.invoke('GetRecentLogs');
      expect(Array.isArray(recentLogs) || typeof recentLogs === 'object').toBe(true);

      const recentEvents = await connection.invoke('GetRecentEvents', 5);
      expect(Array.isArray(recentEvents)).toBe(true);

      const recentStrikes = await connection.invoke('GetRecentStrikes', 5);
      expect(Array.isArray(recentStrikes)).toBe(true);
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

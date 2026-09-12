import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { buildDownloadClientPayload } from '../helpers/api/download-client';
import { buildHubConnection, waitForEvent } from '../helpers/api/signalr';
import { expectKeys } from '../helpers/contract';
import { adminTokens } from '../helpers/test-lifecycle';

const HUB_URL = '/api/hubs/health';

function clientPayload(name: string) {
  return buildDownloadClientPayload('qbittorrent', {
    name,
    host: TEST_CONFIG.mocks.downloadClientUrl,
    username: 'admin',
    password: 'admin',
  });
}

test.describe('SignalR health hub', () => {
  test('first sweep of a new client broadcasts HealthStatusChanged', async ({ api }) => {
    const connection = buildHubConnection({
      accessToken: adminTokens().accessToken,
      hubUrl: HUB_URL,
    });
    await connection.start();
    expect(connection.state).toBe('Connected');

    const name = `health-hub-changed-${Date.now()}`;
    let clientId = '';
    try {
      const created = await (await api.downloadClient.create(clientPayload(name))).json();
      clientId = created.id;

      const changed = waitForEvent<Record<string, unknown>>(
        connection,
        'HealthStatusChanged',
        (payload) => payload.clientId === clientId,
      );
      await api.health.triggerCheck();
      const status = await changed;

      expectKeys(status, [
        'clientId', 'clientName', 'clientTypeName', 'errorMessage',
        'isHealthy', 'lastChecked', 'responseTime',
      ]);
      expect(status.clientName).toBe(name);
    } finally {
      if (clientId) {
        await api.downloadClient.delete(clientId);
      }
      await connection.stop();
    }
  });

  test('deleting a cached client broadcasts ClientRemoved', async ({ api }) => {
    const connection = buildHubConnection({
      accessToken: adminTokens().accessToken,
      hubUrl: HUB_URL,
    });
    await connection.start();

    try {
      const created = await (await api.downloadClient.create(clientPayload(`health-hub-removed-${Date.now()}`))).json();
      const clientId: string = created.id;

      // Seeds the health cache so the next sweep can prune the entry.
      await api.health.triggerCheck();

      const removed = waitForEvent<string>(connection, 'ClientRemoved', (id) => id === clientId);
      await api.downloadClient.delete(clientId);
      await api.health.triggerCheck();

      expect(await removed).toBe(clientId);
    } finally {
      await connection.stop();
    }
  });

  test('connection without token is rejected', async () => {
    const connection = buildHubConnection({
      accessToken: '',
      hubUrl: HUB_URL,
    });
    await expect(connection.start()).rejects.toThrow();
  });
});

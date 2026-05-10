import { test, expect, TEST_CONFIG } from '../fixtures/base';
import type { NotificationProviderType } from '../helpers/api/notifications';

interface ProviderCase {
  type: NotificationProviderType;
  payload: Record<string, unknown>;
}

const CASES: ProviderCase[] = [
  {
    type: 'notifiarr',
    payload: { name: 'notifiarr-e2e', apiKey: 'notifiarr-test-apikey-12345', channelId: '12345' },
  },
  {
    type: 'apprise',
    payload: {
      name: 'apprise-e2e',
      mode: 'Api',
      url: TEST_CONFIG.mocks.notifyUrl,
      key: 'apprise-cfg-e2e',
      tags: '',
    },
  },
  {
    type: 'ntfy',
    payload: {
      name: 'ntfy-e2e',
      serverUrl: TEST_CONFIG.mocks.notifyUrl,
      topics: ['cleanuparr-e2e'],
      authenticationType: 'None',
      priority: 'Default',
    },
  },
  {
    type: 'telegram',
    payload: { name: 'telegram-e2e', botToken: 'BOT-TOKEN-12345', chatId: '12345' },
  },
  {
    type: 'discord',
    payload: {
      name: 'discord-e2e',
      webhookUrl: 'https://discord.com/api/webhooks/123456789/abcdefghij',
    },
  },
  {
    type: 'pushover',
    payload: {
      name: 'pushover-e2e',
      apiToken: 'pushover-api-token',
      userKey: 'pushover-user-key',
      devices: [],
    },
  },
  {
    type: 'gotify',
    payload: { name: 'gotify-e2e', serverUrl: TEST_CONFIG.mocks.notifyUrl, applicationToken: 'gotify-app-token' },
  },
];

test.describe('Notifications — CRUD', () => {
  for (const { type, payload } of CASES) {
    test(`${type}: create + list + update + delete`, async ({ api }) => {
      const create = await api.notifications.create(type, payload);
      if (!create.ok) {
        console.error(`${type} create failed:`, create.status, await create.text());
      }
      expect(create.status).toBeLessThan(300);
      const created = await create.json();
      expect(created.id).toBeTruthy();

      const list = await (await api.notifications.list()).json();
      expect(JSON.stringify(list)).toContain(payload.name as string);

      const update = await api.notifications.update(type, created.id, {
        ...payload,
        name: `${payload.name}-renamed`,
      });
      expect(update.ok).toBe(true);

      const del = await api.notifications.delete(created.id);
      expect(del.status).toBe(204);
    });
  }

  test('GET providers requires auth', async ({ anonymousApi }) => {
    const res = await anonymousApi.notifications.list();
    expect(res.status).toBe(401);
  });
});

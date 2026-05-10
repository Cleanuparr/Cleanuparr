import { test, expect, TEST_CONFIG } from '../fixtures/base';
import type { NotificationProviderType } from '../helpers/api/notifications';

interface ProviderCase {
  type: NotificationProviderType;
  payload: Record<string, unknown>;
}

const CASES: ProviderCase[] = [
  {
    type: 'notifiarr',
    payload: { name: 'notifiarr-e2e', apiKey: 'test-key', channel: '12345', events: {} },
  },
  {
    type: 'apprise',
    payload: { name: 'apprise-e2e', url: `${TEST_CONFIG.mocks.notifyUrl}/notify/test`, events: {} },
  },
  {
    type: 'ntfy',
    payload: {
      name: 'ntfy-e2e',
      serverUrl: TEST_CONFIG.mocks.notifyUrl,
      topic: 'cleanuparr-e2e',
      events: {},
    },
  },
  {
    type: 'telegram',
    payload: { name: 'telegram-e2e', botToken: 'BOT:TOKEN', chatId: '12345', events: {} },
  },
  {
    type: 'discord',
    payload: { name: 'discord-e2e', webhookUrl: `${TEST_CONFIG.mocks.notifyUrl}/webhooks/1/abc`, events: {} },
  },
  {
    type: 'pushover',
    payload: { name: 'pushover-e2e', token: 't', userKey: 'u', devices: [], events: {} },
  },
  {
    type: 'gotify',
    payload: { name: 'gotify-e2e', serverUrl: TEST_CONFIG.mocks.notifyUrl, appToken: 'token', events: {} },
  },
];

test.describe('Notifications — CRUD', () => {
  for (const { type, payload } of CASES) {
    test(`${type}: create + list + update + delete`, async ({ api }) => {
      const create = await api.notifications.create(type, payload);
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

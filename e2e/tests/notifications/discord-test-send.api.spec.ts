import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { NotificationStubs } from '../helpers/mocks';

test.describe('Notifications — Discord test send', () => {
  test('POST /notification_providers/discord/test calls webhook on success', async ({ api, mocks }) => {
    await mocks.notify.stub(NotificationStubs.discordWebhookStub());

    const webhookUrl = `${TEST_CONFIG.mocks.notifyUrl}/webhooks/123/abc`;
    const res = await api.notifications.test('discord', {
      name: 'discord-test',
      webhookUrl,
      events: {},
    });
    expect(res.ok).toBe(true);

    const requests = await mocks.notify.findRequests({
      method: 'POST',
      urlPattern: '/webhooks/.*',
    });
    expect(requests.length).toBeGreaterThan(0);
  });

  test('POST returns failure when webhook URL is unreachable', async ({ api }) => {
    const res = await api.notifications.test('discord', {
      name: 'discord-bad',
      webhookUrl: 'http://127.0.0.1:1/webhooks/x/y',
      events: {},
    });
    expect(res.ok).toBe(false);
  });
});

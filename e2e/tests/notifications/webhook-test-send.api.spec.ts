import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { NotificationStubs } from '../helpers/mocks';

const WEBHOOK_URL = `${TEST_CONFIG.mocks.notifyUrl}/hook`;

test.describe('Notifications: Webhook test send', () => {
  test('POST delivers the payload with the configured method and header', async ({ api, mocks }) => {
    await mocks.notify.stub(NotificationStubs.webhookStub());

    const res = await api.notifications.test('webhook', {
      name: 'webhook-test',
      url: WEBHOOK_URL,
      method: 'Post',
      headers: ['X-Api-Key: webhook-secret'],
    });
    expect(res.status).toBe(200);

    const request = await mocks.notify.waitForRequest({ method: 'POST', urlPath: '/hook' });
    expect(request.headers['X-Api-Key']).toBe('webhook-secret');
    expect(request.headers['Content-Type']).toContain('application/json');

    const body = JSON.parse(request.body ?? '');
    expect(Object.keys(body).sort()).toEqual(
      ['data', 'description', 'eventType', 'image', 'severity', 'timestamp', 'title'].sort(),
    );
    expect(body.eventType).toBe('test');
    expect(body.severity).toBe('information');
    expect(body.title).toBe('Test Notification from Cleanuparr');
    expect(Object.keys(body.data).sort()).toEqual(['providerType', 'testTime']);
    expect(body.data.providerType).toBe('Webhook');
    expect(Number.isNaN(Date.parse(body.timestamp))).toBe(false);
  });

  test('PUT method sends a PUT request', async ({ api, mocks }) => {
    await mocks.notify.stub(NotificationStubs.webhookStub());

    const res = await api.notifications.test('webhook', {
      name: 'webhook-test-put',
      url: WEBHOOK_URL,
      method: 'Put',
      headers: [],
    });
    expect(res.status).toBe(200);

    await mocks.notify.waitForRequest({ method: 'PUT', urlPath: '/hook' });
  });
});

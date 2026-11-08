import { test, expect, TEST_CONFIG } from '../fixtures/base';

const SINK_PATH = '/apprise-cli';

function cliRequest(serviceUrls: string): Record<string, unknown> {
  return { name: 'apprise-cli-e2e', mode: 'Cli', serviceUrls };
}

test.describe('Notifications — Apprise CLI test send', () => {
  test('POST delivers the test notification through the bundled CLI', async ({ api, mocks }) => {
    await mocks.notify.stub({
      request: { method: 'POST', urlPath: SINK_PATH },
      response: { status: 200 },
    });
    const sinkUrl = TEST_CONFIG.mocks.notifyUrl.replace(/^http/, 'json') + SINK_PATH;

    const res = await api.notifications.test('apprise', cliRequest(sinkUrl));
    expect(res.ok, await res.text()).toBe(true);

    const request = await mocks.notify.waitForRequest({ method: 'POST', urlPath: SINK_PATH });
    const payload = JSON.parse(request.body ?? '{}');
    expect(payload.title).toBe('Test Notification from Cleanuparr');
    expect(payload.message).toContain('This is a test notification');
    expect(payload.type).toBe('info');
    // The test image comes from jsdelivr, so this needs internet.
    expect(payload.attachments).toHaveLength(1);
    expect(payload.attachments[0].mimetype).toBe('image/png');
  });

  test('POST returns failure when the CLI cannot reach the service', async ({ api }) => {
    const res = await api.notifications.test('apprise', cliRequest('json://localhost:1/unreachable'));
    expect(res.ok).toBe(false);
  });
});

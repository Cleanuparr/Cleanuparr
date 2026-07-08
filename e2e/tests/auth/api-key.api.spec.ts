import { test, expect, TEST_CONFIG, CleanuparrApi } from '../fixtures/base';

test.describe('Account — API key', () => {
  test('GET returns the current key', async ({ api }) => {
    const res = await api.account.getApiKey();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(typeof body.apiKey).toBe('string');
    expect(body.apiKey.length).toBeGreaterThan(8);
  });

  test('regenerate returns a new key', async ({ api }) => {
    const before = await (await api.account.getApiKey()).json();
    const regen = await api.account.regenerateApiKey();
    expect(regen.status).toBe(200);
    const after = await regen.json();
    expect(after.apiKey).toBeTruthy();
    expect(after.apiKey).not.toBe(before.apiKey);
  });

  test('X-API-Key header authenticates requests', async ({ api }) => {
    const apiKey = (await (await api.account.getApiKey()).json()).apiKey as string;

    const res = await fetch(`${TEST_CONFIG.appUrl}/api/configuration/general`, {
      headers: { 'X-API-Key': apiKey },
    });
    expect(res.status).toBe(200);
  });

  test('regenerating invalidates the old key', async ({ api }) => {
    const oldKey = (await (await api.account.getApiKey()).json()).apiKey as string;
    await api.account.regenerateApiKey();

    const res = await fetch(`${TEST_CONFIG.appUrl}/api/configuration/general`, {
      headers: { 'X-API-Key': oldKey },
    });
    expect(res.status).toBe(401);
  });
});

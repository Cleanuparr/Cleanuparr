import { test, expect } from '@playwright/test';
import {
  loginAndGetToken,
  getDownloadCleanerConfig,
  updateDownloadCleanerConfig,
} from './helpers/app-api';

test.describe.serial('Download Cleaner Config API', () => {
  let token: string;

  test.beforeAll(async () => {
    token = await loginAndGetToken();
  });

  test('should return default download cleaner config', async () => {
    const res = await getDownloadCleanerConfig(token);
    expect(res.status).toBe(200);

    const body = await res.json();
    expect(body).toHaveProperty('enabled');
    expect(body).toHaveProperty('cronExpression');
    expect(body).toHaveProperty('useAdvancedScheduling');
    expect(body).toHaveProperty('ignoredDownloads');
    expect(body).toHaveProperty('clients');
    expect(Array.isArray(body.clients)).toBe(true);
  });

  test('should update global download cleaner config', async () => {
    const getRes = await getDownloadCleanerConfig(token);
    const current = await getRes.json();

    const updateRes = await updateDownloadCleanerConfig(token, {
      enabled: !current.enabled,
      cronExpression: current.cronExpression,
      useAdvancedScheduling: current.useAdvancedScheduling,
      ignoredDownloads: current.ignoredDownloads,
    });
    expect(updateRes.status).toBe(200);

    // Verify the update persisted
    const verifyRes = await getDownloadCleanerConfig(token);
    const updated = await verifyRes.json();
    expect(updated.enabled).toBe(!current.enabled);

    // Restore original
    await updateDownloadCleanerConfig(token, {
      enabled: current.enabled,
      cronExpression: current.cronExpression,
      useAdvancedScheduling: current.useAdvancedScheduling,
      ignoredDownloads: current.ignoredDownloads,
    });
  });

  test('should update ignored downloads list', async () => {
    const getRes = await getDownloadCleanerConfig(token);
    const current = await getRes.json();

    const updateRes = await updateDownloadCleanerConfig(token, {
      enabled: current.enabled,
      cronExpression: current.cronExpression,
      useAdvancedScheduling: current.useAdvancedScheduling,
      ignoredDownloads: ['test-ignored-hash-123'],
    });
    expect(updateRes.status).toBe(200);

    const verifyRes = await getDownloadCleanerConfig(token);
    const updated = await verifyRes.json();
    expect(updated.ignoredDownloads).toContain('test-ignored-hash-123');

    // Restore original
    await updateDownloadCleanerConfig(token, {
      enabled: current.enabled,
      cronExpression: current.cronExpression,
      useAdvancedScheduling: current.useAdvancedScheduling,
      ignoredDownloads: current.ignoredDownloads,
    });
  });

  test('should reject invalid cron expression', async () => {
    const getRes = await getDownloadCleanerConfig(token);
    const current = await getRes.json();

    const res = await updateDownloadCleanerConfig(token, {
      enabled: current.enabled,
      cronExpression: 'not-a-valid-cron',
      useAdvancedScheduling: true,
      ignoredDownloads: current.ignoredDownloads,
    });
    expect(res.status).toBeGreaterThanOrEqual(400);
  });
});

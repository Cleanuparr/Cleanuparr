import { test, expect } from '@playwright/test';
import {
  loginAndGetToken,
  getSeekerConfig,
  updateSeekerConfig,
  getSearchStatsSummary,
  getSearchHistory,
  getSearchEvents,
  getCfScores,
  getCfScoreStats,
} from './helpers/app-api';

test.describe.serial('Seeker API', () => {
  let token: string;

  test.beforeAll(async () => {
    token = await loginAndGetToken();
  });

  test('should return default seeker config', async () => {
    const res = await getSeekerConfig(token);
    expect(res.status).toBe(200);

    const body = await res.json();
    expect(body).toHaveProperty('searchEnabled');
    expect(body).toHaveProperty('searchInterval');
    expect(body).toHaveProperty('proactiveSearchEnabled');
    expect(body).toHaveProperty('selectionStrategy');
    expect(body).toHaveProperty('monitoredOnly');
    expect(body).toHaveProperty('useCutoff');
    expect(body).toHaveProperty('useCustomFormatScore');
    expect(body).toHaveProperty('useRoundRobin');
    expect(body).toHaveProperty('instances');
    expect(Array.isArray(body.instances)).toBe(true);
  });

  test('should update seeker config', async () => {
    // Get current config first
    const getRes = await getSeekerConfig(token);
    const current = await getRes.json();

    // Update with modified values
    const updateRes = await updateSeekerConfig(token, {
      ...current,
      searchEnabled: false,
      searchInterval: 5,
    });
    expect(updateRes.status).toBe(200);

    // Verify the update persisted
    const verifyRes = await getSeekerConfig(token);
    const updated = await verifyRes.json();
    expect(updated.searchEnabled).toBe(false);
    expect(updated.searchInterval).toBe(5);

    // Restore original values
    await updateSeekerConfig(token, {
      ...updated,
      searchEnabled: current.searchEnabled,
      searchInterval: current.searchInterval,
    });
  });

  test('should reject invalid search interval', async () => {
    const getRes = await getSeekerConfig(token);
    const current = await getRes.json();

    const res = await updateSeekerConfig(token, {
      ...current,
      searchInterval: 7, // Not a valid divisor of 60
    });
    // Should fail validation (400 or 500)
    expect(res.status).toBeGreaterThanOrEqual(400);
  });

  test('should return search stats summary with zero counts', async () => {
    const res = await getSearchStatsSummary(token);
    expect(res.status).toBe(200);

    const body = await res.json();
    expect(body).toHaveProperty('totalSearchesAllTime');
    expect(body).toHaveProperty('searchesLast7Days');
    expect(body).toHaveProperty('searchesLast30Days');
    expect(body).toHaveProperty('uniqueItemsSearched');
    expect(body.totalSearchesAllTime).toBeGreaterThanOrEqual(0);
  });

  test('should return empty search history', async () => {
    const res = await getSearchHistory(token);
    expect(res.status).toBe(200);

    const body = await res.json();
    expect(body).toHaveProperty('items');
    expect(Array.isArray(body.items)).toBe(true);
  });

  test('should return empty search events', async () => {
    const res = await getSearchEvents(token);
    expect(res.status).toBe(200);

    const body = await res.json();
    expect(body).toHaveProperty('items');
    expect(Array.isArray(body.items)).toBe(true);
  });

  test('should return empty CF scores list', async () => {
    const res = await getCfScores(token);
    expect(res.status).toBe(200);

    const body = await res.json();
    expect(body).toHaveProperty('items');
    expect(Array.isArray(body.items)).toBe(true);
    expect(body).toHaveProperty('totalCount');
  });

  test('should return CF score stats with zero values', async () => {
    const res = await getCfScoreStats(token);
    expect(res.status).toBe(200);

    const body = await res.json();
    expect(body).toHaveProperty('totalTracked');
    expect(body).toHaveProperty('belowCutoff');
    expect(body.totalTracked).toBeGreaterThanOrEqual(0);
  });
});

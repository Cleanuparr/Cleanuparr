import { test, expect } from '@playwright/test';
import {
  loginAndGetToken,
  getSeekerConfig,
  updateSeekerConfig,
  getSearchStatsSummary,
  getSearchEvents,
  getCfScores,
  getCfScoreStats,
  createArrInstance,
  deleteArrInstance,
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
    expect(body).toHaveProperty('useRoundRobin');
    expect(body).toHaveProperty('postReleaseGraceHours');
    expect(body).toHaveProperty('instances');
    expect(Array.isArray(body.instances)).toBe(true);

    // monitoredOnly, useCutoff, useCustomFormatScore are per-instance settings
    expect(body).not.toHaveProperty('monitoredOnly');
    expect(body).not.toHaveProperty('useCutoff');
    expect(body).not.toHaveProperty('useCustomFormatScore');
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

test.describe.serial('Seeker Per-Instance Config', () => {
  let token: string;
  let radarrId: string;
  let sonarrId: string;

  test.beforeAll(async () => {
    token = await loginAndGetToken();

    // Create Radarr and Sonarr instances for per-instance testing
    const radarrRes = await createArrInstance(token, 'radarr', {
      name: 'E2E Radarr',
      url: 'http://radarr-fake:7878',
      apiKey: 'e2e-test-key-radarr',
      version: 5,
    });
    expect(radarrRes.status).toBe(201);
    const radarrBody = await radarrRes.json();
    radarrId = radarrBody.id;

    const sonarrRes = await createArrInstance(token, 'sonarr', {
      name: 'E2E Sonarr',
      url: 'http://sonarr-fake:8989',
      apiKey: 'e2e-test-key-sonarr',
      version: 4,
    });
    expect(sonarrRes.status).toBe(201);
    const sonarrBody = await sonarrRes.json();
    sonarrId = sonarrBody.id;
  });

  test.afterAll(async () => {
    // Clean up created instances
    if (radarrId) await deleteArrInstance(token, 'radarr', radarrId);
    if (sonarrId) await deleteArrInstance(token, 'sonarr', sonarrId);
  });

  test('should include arr instances with per-instance settings', async () => {
    const res = await getSeekerConfig(token);
    expect(res.status).toBe(200);

    const body = await res.json();
    expect(body.instances.length).toBeGreaterThanOrEqual(2);

    const radarr = body.instances.find((i: Record<string, unknown>) => i.arrInstanceId === radarrId);
    const sonarr = body.instances.find((i: Record<string, unknown>) => i.arrInstanceId === sonarrId);

    expect(radarr).toBeDefined();
    expect(sonarr).toBeDefined();

    // Verify per-instance properties exist with defaults
    for (const instance of [radarr, sonarr]) {
      expect(instance).toHaveProperty('enabled');
      expect(instance).toHaveProperty('skipTags');
      expect(instance).toHaveProperty('activeDownloadLimit');
      expect(instance).toHaveProperty('minCycleTimeDays');
      expect(instance).toHaveProperty('monitoredOnly');
      expect(instance).toHaveProperty('useCutoff');
      expect(instance).toHaveProperty('useCustomFormatScore');
      expect(instance).toHaveProperty('instanceName');
      expect(instance).toHaveProperty('instanceType');
      expect(instance).toHaveProperty('arrInstanceEnabled');
    }

    // Defaults: monitoredOnly=true, useCutoff=false, useCustomFormatScore=false
    expect(radarr.monitoredOnly).toBe(true);
    expect(radarr.useCutoff).toBe(false);
    expect(radarr.useCustomFormatScore).toBe(false);
  });

  test('should update per-instance settings independently', async () => {
    const getRes = await getSeekerConfig(token);
    const current = await getRes.json();

    // Set different settings per instance:
    // Radarr: useCutoff + useCustomFormatScore, monitoredOnly off
    // Sonarr: useCutoff only, monitoredOnly on
    const instances = current.instances.map((i: Record<string, unknown>) => {
      if (i.arrInstanceId === radarrId) {
        return { ...i, enabled: true, monitoredOnly: false, useCutoff: true, useCustomFormatScore: true };
      }
      if (i.arrInstanceId === sonarrId) {
        return { ...i, enabled: true, monitoredOnly: true, useCutoff: true, useCustomFormatScore: false };
      }
      return i;
    });

    const updateRes = await updateSeekerConfig(token, {
      ...current,
      instances,
    });
    expect(updateRes.status).toBe(200);

    // Verify settings persisted with correct per-instance values
    const verifyRes = await getSeekerConfig(token);
    const updated = await verifyRes.json();

    const radarr = updated.instances.find((i: Record<string, unknown>) => i.arrInstanceId === radarrId);
    const sonarr = updated.instances.find((i: Record<string, unknown>) => i.arrInstanceId === sonarrId);

    expect(radarr.monitoredOnly).toBe(false);
    expect(radarr.useCutoff).toBe(true);
    expect(radarr.useCustomFormatScore).toBe(true);

    expect(sonarr.monitoredOnly).toBe(true);
    expect(sonarr.useCutoff).toBe(true);
    expect(sonarr.useCustomFormatScore).toBe(false);
  });

  test('should persist per-instance settings across updates', async () => {
    // Update only a global setting without changing instances
    const getRes = await getSeekerConfig(token);
    const current = await getRes.json();

    const updateRes = await updateSeekerConfig(token, {
      ...current,
      postReleaseGraceHours: 12,
    });
    expect(updateRes.status).toBe(200);

    // Per-instance settings should remain unchanged
    const verifyRes = await getSeekerConfig(token);
    const updated = await verifyRes.json();

    const radarr = updated.instances.find((i: Record<string, unknown>) => i.arrInstanceId === radarrId);
    expect(radarr.useCutoff).toBe(true);
    expect(radarr.useCustomFormatScore).toBe(true);
    expect(updated.postReleaseGraceHours).toBe(12);

    // Restore
    await updateSeekerConfig(token, {
      ...updated,
      postReleaseGraceHours: current.postReleaseGraceHours,
    });
  });
});

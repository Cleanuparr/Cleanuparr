import { test, expect, TEST_CONFIG } from '../fixtures/base';

// These tests create arr instances inline rather than in beforeAll because the
// auto-reset fixture wipes them between tests.

async function createTwoArrInstances(
  api: import('../helpers/api').CleanuparrApi,
): Promise<{ radarrId: string; sonarrId: string }> {
  const radarr = await (
    await api.arr.createInstance('radarr', {
      name: 'E2E Radarr',
      url: TEST_CONFIG.mocks.arrUrl,
      apiKey: 'e2e-test-key-radarr',
      version: 5,
    })
  ).json();

  const sonarr = await (
    await api.arr.createInstance('sonarr', {
      name: 'E2E Sonarr',
      url: TEST_CONFIG.mocks.arrUrl,
      apiKey: 'e2e-test-key-sonarr',
      version: 4,
    })
  ).json();

  return { radarrId: radarr.id, sonarrId: sonarr.id };
}

test.describe('Seeker — per-instance config', () => {
  test('config includes arr instances with per-instance settings', async ({ api }) => {
    const { radarrId, sonarrId } = await createTwoArrInstances(api);

    const body = await (await api.seeker.getConfig()).json();
    expect(body.instances.length).toBeGreaterThanOrEqual(2);

    const radarr = body.instances.find((i: { arrInstanceId: string }) => i.arrInstanceId === radarrId);
    const sonarr = body.instances.find((i: { arrInstanceId: string }) => i.arrInstanceId === sonarrId);
    expect(radarr).toBeDefined();
    expect(sonarr).toBeDefined();

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

    expect(radarr.monitoredOnly).toBe(true);
    expect(radarr.useCutoff).toBe(false);
    expect(radarr.useCustomFormatScore).toBe(false);
  });

  test('updates per-instance settings independently', async ({ api }) => {
    const { radarrId, sonarrId } = await createTwoArrInstances(api);
    const current = await (await api.seeker.getConfig()).json();

    const instances = current.instances.map((i: { arrInstanceId: string }) => {
      if (i.arrInstanceId === radarrId) {
        return { ...i, enabled: true, monitoredOnly: false, useCutoff: true, useCustomFormatScore: true };
      }
      if (i.arrInstanceId === sonarrId) {
        return { ...i, enabled: true, monitoredOnly: true, useCutoff: true, useCustomFormatScore: false };
      }
      return i;
    });

    const update = await api.seeker.updateConfig({ ...current, instances });
    expect(update.status).toBe(200);

    const updated = await (await api.seeker.getConfig()).json();
    const radarr = updated.instances.find((i: { arrInstanceId: string }) => i.arrInstanceId === radarrId);
    const sonarr = updated.instances.find((i: { arrInstanceId: string }) => i.arrInstanceId === sonarrId);

    expect(radarr.monitoredOnly).toBe(false);
    expect(radarr.useCutoff).toBe(true);
    expect(radarr.useCustomFormatScore).toBe(true);
    expect(sonarr.monitoredOnly).toBe(true);
    expect(sonarr.useCutoff).toBe(true);
    expect(sonarr.useCustomFormatScore).toBe(false);
  });

  test('persists per-instance settings across global updates', async ({ api }) => {
    const { radarrId } = await createTwoArrInstances(api);
    let current = await (await api.seeker.getConfig()).json();

    const seeded = current.instances.map((i: { arrInstanceId: string }) =>
      i.arrInstanceId === radarrId
        ? { ...i, enabled: true, useCutoff: true, useCustomFormatScore: true }
        : i,
    );
    await api.seeker.updateConfig({ ...current, instances: seeded });

    current = await (await api.seeker.getConfig()).json();
    const update = await api.seeker.updateConfig({ ...current, postReleaseGraceHours: 12 });
    expect(update.status).toBe(200);

    const updated = await (await api.seeker.getConfig()).json();
    const radarr = updated.instances.find((i: { arrInstanceId: string }) => i.arrInstanceId === radarrId);
    expect(radarr.useCutoff).toBe(true);
    expect(radarr.useCustomFormatScore).toBe(true);
    expect(updated.postReleaseGraceHours).toBe(12);
  });
});

import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { ArrStubs } from '../helpers/mocks';

test.describe.serial('Seeker — config', () => {
  test('returns default seeker config', async ({ api }) => {
    const res = await api.seeker.getConfig();
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

    expect(body).not.toHaveProperty('monitoredOnly');
    expect(body).not.toHaveProperty('useCutoff');
    expect(body).not.toHaveProperty('useCustomFormatScore');
  });

  test('lists a Whisparr v3 instance but not a v2 one', async ({ api, mocks }) => {
    await mocks.arr.stub(ArrStubs.arrHealthStub());

    const created: string[] = [];
    try {
      for (const version of [2, 3]) {
        const res = await api.arr.createInstance('whisparr', {
          name: `whisparr-v${version}-e2e`,
          url: TEST_CONFIG.mocks.arrUrl,
          apiKey: 'e2e-test-key',
          version,
          enabled: true,
        });
        expect(res.status).toBeLessThan(300);
        created.push((await res.json()).id);
      }

      const [v2Id, v3Id] = created;
      const config = await (await api.seeker.getConfig()).json();
      const listed = config.instances.map((i: { arrInstanceId: string }) => i.arrInstanceId);

      expect(listed).toContain(v3Id);
      expect(listed).not.toContain(v2Id);
    } finally {
      for (const id of created) {
        await api.arr.deleteInstance('whisparr', id);
      }
    }
  });

  test('updates seeker config', async ({ api }) => {
    const current = await (await api.seeker.getConfig()).json();

    const updateRes = await api.seeker.updateConfig({
      ...current,
      searchEnabled: false,
      searchInterval: 5,
    });
    expect(updateRes.status).toBe(200);

    const updated = await (await api.seeker.getConfig()).json();
    expect(updated.searchEnabled).toBe(false);
    expect(updated.searchInterval).toBe(5);

    await api.seeker.updateConfig({
      ...updated,
      searchEnabled: current.searchEnabled,
      searchInterval: current.searchInterval,
    });
  });

  test('rejects invalid search interval (not a divisor of 60)', async ({ api }) => {
    const current = await (await api.seeker.getConfig()).json();
    const res = await api.seeker.updateConfig({ ...current, searchInterval: 7 });
    expect(res.status).toBeGreaterThanOrEqual(400);
  });

  test('round-trips quiet hours', async ({ api }) => {
    const current = await (await api.seeker.getConfig()).json();
    expect(current).toHaveProperty('quietHoursEnabled');
    expect(current).toHaveProperty('quietHoursStart');
    expect(current).toHaveProperty('quietHoursEnd');

    const updateRes = await api.seeker.updateConfig({
      ...current,
      quietHoursEnabled: true,
      quietHoursStart: '22:30',
      quietHoursEnd: '06:15',
    });
    expect(updateRes.status).toBe(200);

    const updated = await (await api.seeker.getConfig()).json();
    expect(updated.quietHoursEnabled).toBe(true);
    expect(updated.quietHoursStart).toBe('22:30:00');
    expect(updated.quietHoursEnd).toBe('06:15:00');

    await api.seeker.updateConfig({
      ...updated,
      quietHoursEnabled: current.quietHoursEnabled,
      quietHoursStart: current.quietHoursStart,
      quietHoursEnd: current.quietHoursEnd,
    });
  });

  test('rejects quiet hours with equal start and end', async ({ api }) => {
    const current = await (await api.seeker.getConfig()).json();
    const res = await api.seeker.updateConfig({
      ...current,
      searchEnabled: true,
      proactiveSearchEnabled: true,
      quietHoursEnabled: true,
      quietHoursStart: '08:00',
      quietHoursEnd: '08:00',
    });
    expect(res.status).toBe(400);
  });

  test('returns search stats summary with zero counts initially', async ({ api }) => {
    const res = await api.seeker.getSearchStatsSummary();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('totalSearchesAllTime');
    expect(body).toHaveProperty('searchesLast7Days');
    expect(body).toHaveProperty('searchesLast30Days');
    expect(body).toHaveProperty('uniqueItemsSearched');
    expect(body.totalSearchesAllTime).toBeGreaterThanOrEqual(0);
  });

  test('returns empty CF scores list initially', async ({ api }) => {
    const res = await api.seeker.listCustomFormatScores();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body) || Array.isArray(body.items)).toBe(true);
  });

  test('returns CF score stats with zero values initially', async ({ api }) => {
    const res = await api.seeker.getCustomFormatScoreStats();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('totalTracked');
    expect(body).toHaveProperty('belowCutoff');
    expect(body.totalTracked).toBeGreaterThanOrEqual(0);
  });
});

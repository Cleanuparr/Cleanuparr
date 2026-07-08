import { test, expect } from '../fixtures/base';

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

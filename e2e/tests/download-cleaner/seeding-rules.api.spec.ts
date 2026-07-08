import { test, expect } from '../fixtures/base';
import { buildDownloadClientPayload } from '../helpers/api/download-client';

test.describe.serial('DownloadCleaner — seeding rules CRUD', () => {
  let downloadClientId: string;

  test.beforeAll(async ({ api }) => {
    const res = await api.downloadClient.create(
      buildDownloadClientPayload('qbittorrent', {
        name: 'e2e-test-qbit',
        host: 'http://127.0.0.1:9999',
        enabled: false,
      }),
    );
    expect(res.status).toBe(201);
    const client = await res.json();
    downloadClientId = client.id;
  });

  test.afterAll(async ({ api }) => {
    if (downloadClientId) {
      await api.downloadClient.delete(downloadClientId);
    }
  });

  test('returns empty rules for a new client', async ({ api }) => {
    const res = await api.downloadCleaner.listSeedingRules(downloadClientId);
    expect(res.status).toBe(200);
    const rules = await res.json();
    expect(Array.isArray(rules)).toBe(true);
    expect(rules).toHaveLength(0);
  });

  test('creates a seeding rule with new fields', async ({ api }) => {
    const res = await api.downloadCleaner.createSeedingRule(downloadClientId, {
      name: 'Movies Rule',
      categories: ['movies', 'films'],
      trackerPatterns: ['tracker.example.com'],
      tagsAny: ['hd'],
      tagsAll: [],
      privacyType: 'Both',
      maxRatio: 2.0,
      minSeedTime: 0,
      maxSeedTime: -1,
      minSeeders: 5,
      deleteSourceFiles: true,
    });
    expect(res.status).toBe(201);

    const rule = await res.json();
    expect(rule.name).toBe('Movies Rule');
    expect(rule.categories).toEqual(['movies', 'films']);
    expect(rule.trackerPatterns).toEqual(['tracker.example.com']);
    expect(rule.tagsAny).toEqual(['hd']);
    expect(rule.tagsAll).toEqual([]);
    expect(rule.priority).toBe(1);
  });

  test('auto-assigns sequential priorities', async ({ api }) => {
    const res2 = await api.downloadCleaner.createSeedingRule(downloadClientId, {
      name: 'TV Rule',
      categories: ['tv'],
      privacyType: 'Both',
      maxRatio: -1,
      minSeedTime: 0,
      maxSeedTime: 48,
      deleteSourceFiles: true,
    });
    expect(res2.status).toBe(201);
    expect((await res2.json()).priority).toBe(2);

    const res3 = await api.downloadCleaner.createSeedingRule(downloadClientId, {
      name: 'Music Rule',
      categories: ['music'],
      privacyType: 'Both',
      maxRatio: 3.0,
      minSeedTime: 0,
      maxSeedTime: -1,
      deleteSourceFiles: false,
    });
    expect(res3.status).toBe(201);
    expect((await res3.json()).priority).toBe(3);
  });

  test('round-trips new fields through GET', async ({ api }) => {
    const rules = await (await api.downloadCleaner.listSeedingRules(downloadClientId)).json();
    expect(rules).toHaveLength(3);

    const moviesRule = rules.find((r: { name: string }) => r.name === 'Movies Rule');
    expect(moviesRule).toBeDefined();
    expect(moviesRule.categories).toEqual(['movies', 'films']);
    expect(moviesRule.trackerPatterns).toEqual(['tracker.example.com']);
    expect(moviesRule.minSeeders).toBe(5);
    expect(moviesRule.priority).toBe(1);
  });

  test('reorders seeding rules', async ({ api }) => {
    const rules = await (await api.downloadCleaner.listSeedingRules(downloadClientId)).json();
    expect(rules).toHaveLength(3);

    const reversedIds = rules.map((r: { id: string }) => r.id).reverse();
    const reorderRes = await api.downloadCleaner.reorderSeedingRules(downloadClientId, reversedIds);
    expect(reorderRes.status).toBe(204);

    const reordered = await (await api.downloadCleaner.listSeedingRules(downloadClientId)).json();
    expect(reordered[0].priority).toBe(1);
    expect(reordered[0].id).toBe(reversedIds[0]);
    expect(reordered[1].priority).toBe(2);
    expect(reordered[1].id).toBe(reversedIds[1]);
    expect(reordered[2].priority).toBe(3);
    expect(reordered[2].id).toBe(reversedIds[2]);
  });

  test('rejects reorder with missing rule IDs', async ({ api }) => {
    const rules = await (await api.downloadCleaner.listSeedingRules(downloadClientId)).json();
    const partialIds = rules.slice(0, 2).map((r: { id: string }) => r.id);
    const res = await api.downloadCleaner.reorderSeedingRules(downloadClientId, partialIds);
    expect(res.status).toBeGreaterThanOrEqual(400);
  });

  test('rejects reorder with duplicate IDs', async ({ api }) => {
    const rules = await (await api.downloadCleaner.listSeedingRules(downloadClientId)).json();
    const firstId = rules[0].id;
    const res = await api.downloadCleaner.reorderSeedingRules(downloadClientId, [firstId, firstId, rules[1].id]);
    expect(res.status).toBeGreaterThanOrEqual(400);
  });

  test('rejects reorder with invalid rule ID', async ({ api }) => {
    const rules = await (await api.downloadCleaner.listSeedingRules(downloadClientId)).json();
    const ids = rules.map((r: { id: string }) => r.id);
    ids[0] = '00000000-0000-0000-0000-000000000000';
    const res = await api.downloadCleaner.reorderSeedingRules(downloadClientId, ids);
    expect(res.status).toBeGreaterThanOrEqual(400);
  });

  test('does not change priority on update', async ({ api }) => {
    const rules = await (await api.downloadCleaner.listSeedingRules(downloadClientId)).json();
    const rule = rules[0];

    const updateRes = await api.downloadCleaner.updateSeedingRule(rule.id, {
      name: 'Updated Name',
      categories: rule.categories,
      trackerPatterns: rule.trackerPatterns,
      privacyType: rule.privacyType,
      maxRatio: rule.maxRatio,
      minSeedTime: rule.minSeedTime,
      maxSeedTime: rule.maxSeedTime,
      minSeeders: rule.minSeeders,
      deleteSourceFiles: rule.deleteSourceFiles,
    });
    expect(updateRes.status).toBe(200);
    expect((await updateRes.json()).priority).toBe(rule.priority);
  });

  test('updates tags and persists them', async ({ api }) => {
    const rules = await (await api.downloadCleaner.listSeedingRules(downloadClientId)).json();
    const rule = rules[0];

    const updateRes = await api.downloadCleaner.updateSeedingRule(rule.id, {
      name: rule.name,
      categories: rule.categories,
      trackerPatterns: rule.trackerPatterns,
      tagsAny: ['updated-tag-1', 'updated-tag-2'],
      tagsAll: ['required-tag'],
      privacyType: rule.privacyType,
      maxRatio: rule.maxRatio,
      minSeedTime: rule.minSeedTime,
      maxSeedTime: rule.maxSeedTime,
      minSeeders: rule.minSeeders,
      deleteSourceFiles: rule.deleteSourceFiles,
    });
    expect(updateRes.status).toBe(200);

    const updated = await (await api.downloadCleaner.listSeedingRules(downloadClientId)).json();
    const updatedRule = updated.find((r: { id: string }) => r.id === rule.id);
    expect(updatedRule.tagsAny).toEqual(['updated-tag-1', 'updated-tag-2']);
    expect(updatedRule.tagsAll).toEqual(['required-tag']);
  });

  test('rejects empty categories', async ({ api }) => {
    const res = await api.downloadCleaner.createSeedingRule(downloadClientId, {
      name: 'Bad Rule',
      categories: [],
      privacyType: 'Both',
      maxRatio: -1,
      minSeedTime: 0,
      maxSeedTime: -1,
      deleteSourceFiles: true,
    });
    expect(res.status).toBeGreaterThanOrEqual(400);
  });

  test('rejects negative priority', async ({ api }) => {
    const res = await api.downloadCleaner.createSeedingRule(downloadClientId, {
      name: 'Bad Priority Rule',
      categories: ['test'],
      priority: -1,
      privacyType: 'Both',
      maxRatio: -1,
      minSeedTime: 0,
      maxSeedTime: -1,
      deleteSourceFiles: true,
    });
    expect(res.status).toBeGreaterThanOrEqual(400);
  });

  test('strips empty tracker patterns', async ({ api }) => {
    const res = await api.downloadCleaner.createSeedingRule(downloadClientId, {
      name: 'Whitespace Test',
      categories: ['test'],
      trackerPatterns: ['', '  ', 'valid.com'],
      privacyType: 'Both',
      maxRatio: 2.0,
      minSeedTime: 0,
      maxSeedTime: -1,
      deleteSourceFiles: true,
    });
    expect(res.status).toBe(201);
    const rule = await res.json();
    expect(rule.trackerPatterns).toEqual(['valid.com']);

    await api.downloadCleaner.deleteSeedingRule(rule.id);
  });

  test('deletes a seeding rule', async ({ api }) => {
    const rules = await (await api.downloadCleaner.listSeedingRules(downloadClientId)).json();
    const lastRule = rules[rules.length - 1];

    const delRes = await api.downloadCleaner.deleteSeedingRule(lastRule.id);
    expect(delRes.status).toBe(204);

    const remaining = await (await api.downloadCleaner.listSeedingRules(downloadClientId)).json();
    expect(remaining).toHaveLength(rules.length - 1);
  });

  test('returns 404 for non-existent download client', async ({ api }) => {
    const res = await api.downloadCleaner.listSeedingRules('00000000-0000-0000-0000-000000000000');
    expect(res.status).toBe(404);
  });
});

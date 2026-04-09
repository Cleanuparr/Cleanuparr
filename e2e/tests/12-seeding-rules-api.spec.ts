import { test, expect } from '@playwright/test';
import {
  loginAndGetToken,
  createDownloadClient,
  deleteDownloadClient,
  getSeedingRules,
  createSeedingRule,
  updateSeedingRule,
  deleteSeedingRule,
  reorderSeedingRules,
} from './helpers/app-api';

test.describe.serial('Seeding Rules API', () => {
  let token: string;
  let downloadClientId: string;

  test.beforeAll(async () => {
    token = await loginAndGetToken();

    // Create a qBittorrent download client for testing seeding rules
    const res = await createDownloadClient(token, {
      enabled: false,
      name: 'e2e-test-qbit',
      typeName: 'qBittorrent',
      type: 'Torrent',
      host: 'http://localhost:9999',
    });
    expect(res.status).toBe(201);
    const client = await res.json();
    downloadClientId = client.id;
  });

  test.afterAll(async () => {
    if (downloadClientId) {
      await deleteDownloadClient(token, downloadClientId);
    }
  });

  test('should return empty seeding rules for new client', async () => {
    const res = await getSeedingRules(token, downloadClientId);
    expect(res.status).toBe(200);
    const rules = await res.json();
    expect(Array.isArray(rules)).toBe(true);
    expect(rules).toHaveLength(0);
  });

  test('should create a seeding rule with new fields', async () => {
    const res = await createSeedingRule(token, downloadClientId, {
      name: 'Movies Rule',
      categories: ['movies', 'films'],
      trackerPatterns: ['tracker.example.com'],
      tagsAny: ['hd'],
      tagsAll: [],
      privacyType: 'Both',
      maxRatio: 2.0,
      minSeedTime: 0,
      maxSeedTime: -1,
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

  test('should auto-assign sequential priorities', async () => {
    const res2 = await createSeedingRule(token, downloadClientId, {
      name: 'TV Rule',
      categories: ['tv'],
      privacyType: 'Both',
      maxRatio: -1,
      minSeedTime: 0,
      maxSeedTime: 48,
      deleteSourceFiles: true,
    });
    expect(res2.status).toBe(201);
    const rule2 = await res2.json();
    expect(rule2.priority).toBe(2);

    const res3 = await createSeedingRule(token, downloadClientId, {
      name: 'Music Rule',
      categories: ['music'],
      privacyType: 'Both',
      maxRatio: 3.0,
      minSeedTime: 0,
      maxSeedTime: -1,
      deleteSourceFiles: false,
    });
    expect(res3.status).toBe(201);
    const rule3 = await res3.json();
    expect(rule3.priority).toBe(3);
  });

  test('should round-trip new fields through GET', async () => {
    const res = await getSeedingRules(token, downloadClientId);
    expect(res.status).toBe(200);
    const rules = await res.json();
    expect(rules).toHaveLength(3);

    const moviesRule = rules.find((r: { name: string }) => r.name === 'Movies Rule');
    expect(moviesRule).toBeDefined();
    expect(moviesRule.categories).toEqual(['movies', 'films']);
    expect(moviesRule.trackerPatterns).toEqual(['tracker.example.com']);
    expect(moviesRule.priority).toBe(1);
  });

  test('should reorder seeding rules', async () => {
    const getRes = await getSeedingRules(token, downloadClientId);
    const rules = await getRes.json();
    expect(rules).toHaveLength(3);

    // Reverse the order
    const reversedIds = rules.map((r: { id: string }) => r.id).reverse();
    const reorderRes = await reorderSeedingRules(token, downloadClientId, reversedIds);
    expect(reorderRes.status).toBe(204);

    // Verify new order
    const verifyRes = await getSeedingRules(token, downloadClientId);
    const reordered = await verifyRes.json();
    expect(reordered[0].priority).toBe(1);
    expect(reordered[0].id).toBe(reversedIds[0]);
    expect(reordered[1].priority).toBe(2);
    expect(reordered[1].id).toBe(reversedIds[1]);
    expect(reordered[2].priority).toBe(3);
    expect(reordered[2].id).toBe(reversedIds[2]);
  });

  test('should reject reorder with missing rule IDs', async () => {
    const getRes = await getSeedingRules(token, downloadClientId);
    const rules = await getRes.json();

    // Only send 2 of 3 IDs
    const partialIds = rules.slice(0, 2).map((r: { id: string }) => r.id);
    const res = await reorderSeedingRules(token, downloadClientId, partialIds);
    expect(res.status).toBeGreaterThanOrEqual(400);
  });

  test('should reject reorder with duplicate IDs', async () => {
    const getRes = await getSeedingRules(token, downloadClientId);
    const rules = await getRes.json();

    const firstId = rules[0].id;
    const res = await reorderSeedingRules(token, downloadClientId, [firstId, firstId, rules[1].id]);
    expect(res.status).toBeGreaterThanOrEqual(400);
  });

  test('should reject reorder with invalid rule ID', async () => {
    const getRes = await getSeedingRules(token, downloadClientId);
    const rules = await getRes.json();

    const ids = rules.map((r: { id: string }) => r.id);
    ids[0] = '00000000-0000-0000-0000-000000000000';
    const res = await reorderSeedingRules(token, downloadClientId, ids);
    expect(res.status).toBeGreaterThanOrEqual(400);
  });

  test('should not change priority on update', async () => {
    const getRes = await getSeedingRules(token, downloadClientId);
    const rules = await getRes.json();
    const rule = rules[0];

    const updateRes = await updateSeedingRule(token, rule.id, {
      name: 'Updated Name',
      categories: rule.categories,
      trackerPatterns: rule.trackerPatterns,
      privacyType: rule.privacyType,
      maxRatio: rule.maxRatio,
      minSeedTime: rule.minSeedTime,
      maxSeedTime: rule.maxSeedTime,
      deleteSourceFiles: rule.deleteSourceFiles,
    });
    expect(updateRes.status).toBe(200);
    const updated = await updateRes.json();
    expect(updated.priority).toBe(rule.priority);
  });

  test('should reject empty categories', async () => {
    const res = await createSeedingRule(token, downloadClientId, {
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

  test('should reject negative priority', async () => {
    const res = await createSeedingRule(token, downloadClientId, {
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

  test('should strip empty tracker patterns', async () => {
    const res = await createSeedingRule(token, downloadClientId, {
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

    // Clean up
    await deleteSeedingRule(token, rule.id);
  });

  test('should delete a seeding rule', async () => {
    const getRes = await getSeedingRules(token, downloadClientId);
    const rules = await getRes.json();
    const lastRule = rules[rules.length - 1];

    const delRes = await deleteSeedingRule(token, lastRule.id);
    expect(delRes.status).toBe(204);

    const verifyRes = await getSeedingRules(token, downloadClientId);
    const remaining = await verifyRes.json();
    expect(remaining).toHaveLength(rules.length - 1);
  });

  test('should return 404 for non-existent download client', async () => {
    const res = await getSeedingRules(token, '00000000-0000-0000-0000-000000000000');
    expect(res.status).toBe(404);
  });
});

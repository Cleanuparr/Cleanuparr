import { test, expect } from '../fixtures/base';
import type { QueueRuleKind } from '../helpers/api/queue-cleaner';
import { expectKeys } from '../helpers/contract';

const KINDS: QueueRuleKind[] = ['stall', 'slow'];

function buildPayload(kind: QueueRuleKind, name: string, overrides: Record<string, unknown> = {}) {
  const base = {
    name,
    enabled: true,
    maxStrikes: 3,
    privacyType: 'Public',
    minCompletionPercentage: 0,
    maxCompletionPercentage: 100,
    deletePrivateTorrentsFromClient: false,
    changeCategory: false,
  };
  if (kind === 'stall') {
    return { ...base, resetStrikesOnProgress: true, minimumProgress: null, ...overrides };
  }
  return {
    ...base,
    resetStrikesOnProgress: true,
    minSpeed: '100KB',
    maxTimeHours: 0,
    ignoreAboveSize: null,
    ...overrides,
  };
}

const RULE_KEYS: Record<QueueRuleKind, string[]> = {
  stall: [
    'changeCategory', 'contentType', 'deletePrivateTorrentsFromClient', 'enabled', 'id',
    'maxCompletionPercentage', 'maxStrikes', 'minCompletionPercentage', 'minimumProgress',
    'name', 'privacyType', 'resetStrikesOnProgress',
  ],
  slow: [
    'changeCategory', 'contentType', 'deletePrivateTorrentsFromClient', 'enabled', 'id', 'ignoreAboveSize',
    'ignoreWhileAltSpeedActive', 'maxCompletionPercentage', 'maxStrikes', 'maxTimeHours',
    'minCompletionPercentage', 'minSpeed', 'name', 'privacyType', 'resetStrikesOnProgress',
  ],
};

test.describe('QueueCleaner: rules CRUD', () => {
  // Belt-and-braces: SQLite-direct autoReset clears the rule tables, but EF
  // Core's pooled connection can hold a pre-DELETE snapshot.
  // Delete any lingering rules through the API too, so the
  // backend's own view matches the reset.
  test.beforeEach(async ({ api }) => {
    for (const kind of KINDS) {
      const list = await (await api.queueCleaner.listRules(kind)).json();
      if (Array.isArray(list)) {
        for (const rule of list) {
          if (rule?.id) {
            await api.queueCleaner.deleteRule(kind, rule.id);
          }
        }
      }
    }
  });

  for (const kind of KINDS) {
    test(`${kind}: create + list + update + delete`, async ({ api }) => {
      const create = await api.queueCleaner.createRule(kind, buildPayload(kind, `${kind}-e2e`));
      if (!create.ok) {
        console.error(`${kind} create failed:`, create.status, await create.text());
      }
      expect(create.status).toBeLessThan(300);
      const created = await create.json();
      expect(created.id).toBeTruthy();

      const list = await (await api.queueCleaner.listRules(kind)).json();
      expect(list.some((r: { id: string }) => r.id === created.id)).toBe(true);

      const update = await api.queueCleaner.updateRule(kind, created.id, {
        ...created,
        name: `${kind}-renamed`,
        maxStrikes: 5,
      });
      expect(update.ok).toBe(true);

      const del = await api.queueCleaner.deleteRule(kind, created.id);
      expect(del.status).toBe(204);
    });

    test(`${kind}: create, list and update all return the same pinned shape`, async ({ api }) => {
      const created = await (await api.queueCleaner.createRule(kind, buildPayload(kind, `${kind}-shape`))).json();
      expectKeys(created, RULE_KEYS[kind]);

      const listed = await (await api.queueCleaner.listRules(kind)).json();
      expectKeys(listed.find((r: { id: string }) => r.id === created.id), RULE_KEYS[kind]);

      const updated = await (await api.queueCleaner.updateRule(kind, created.id, {
        ...created,
        name: `${kind}-shape-renamed`,
      })).json();
      expectKeys(updated, RULE_KEYS[kind]);

      await api.queueCleaner.deleteRule(kind, created.id);
    });

    test(`${kind}: rejects maxStrikes below the minimum`, async ({ api }) => {
      const res = await api.queueCleaner.createRule(
        kind,
        buildPayload(kind, `${kind}-bad`, { maxStrikes: 1 }),
      );
      expect(res.status).toBeGreaterThanOrEqual(400);
      expect(res.status).toBeLessThan(500);
    });

    test(`${kind}: New and Upgrade rules coexist, but All overlapping either is rejected`, async ({ api }) => {
      const newRule = await api.queueCleaner.createRule(
        kind,
        buildPayload(kind, `${kind}-content-new`, { contentType: 'New' }),
      );
      expect(newRule.status).toBeLessThan(300);

      const upgradeRule = await api.queueCleaner.createRule(
        kind,
        buildPayload(kind, `${kind}-content-upgrade`, { contentType: 'Upgrade' }),
      );
      expect(upgradeRule.status).toBeLessThan(300);

      const allRule = await api.queueCleaner.createRule(
        kind,
        buildPayload(kind, `${kind}-content-all`, { contentType: 'All' }),
      );
      expect(allRule.status).toBeGreaterThanOrEqual(400);
      expect(allRule.status).toBeLessThan(500);
    });
  }
});

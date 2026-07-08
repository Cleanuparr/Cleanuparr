import { test, expect } from '../fixtures/base';
import type { QueueRuleKind } from '../helpers/api/queue-cleaner';

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

test.describe('QueueCleaner — rules CRUD', () => {
  // Belt-and-braces: SQLite-direct autoReset clears the rule tables, but EF
  // Core's pooled connection can occasionally hold a pre-DELETE snapshot.
  // Explicitly delete any lingering rules through the API as well so the
  // backend's own view matches our reset.
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

    test(`${kind}: rejects maxStrikes below the minimum`, async ({ api }) => {
      const res = await api.queueCleaner.createRule(
        kind,
        buildPayload(kind, `${kind}-bad`, { maxStrikes: 1 }),
      );
      expect(res.status).toBeGreaterThanOrEqual(400);
      expect(res.status).toBeLessThan(500);
    });
  }
});

import { test, expect } from '../fixtures/base';
import type { QueueRuleKind } from '../helpers/api/queue-cleaner';

const KINDS: QueueRuleKind[] = ['stall', 'slow'];

test.describe('QueueCleaner — rules CRUD', () => {
  for (const kind of KINDS) {
    test(`${kind}: create + list + update + delete`, async ({ api }) => {
      const create = await api.queueCleaner.createRule(kind, {
        name: `${kind}-e2e`,
        enabled: true,
        maxStrikes: 3,
        applyTo: 'all',
        ...(kind === 'stall'
          ? { stalledDuration: '00:30:00' }
          : { speedThresholdKbps: 100, slowDuration: '00:30:00' }),
      });
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

    test(`${kind}: rejects negative maxStrikes`, async ({ api }) => {
      const res = await api.queueCleaner.createRule(kind, {
        name: `${kind}-bad`,
        enabled: true,
        maxStrikes: -1,
        applyTo: 'all',
        ...(kind === 'stall'
          ? { stalledDuration: '00:30:00' }
          : { speedThresholdKbps: 100, slowDuration: '00:30:00' }),
      });
      expect(res.status).toBeGreaterThanOrEqual(400);
      expect(res.status).toBeLessThan(500);
    });
  }
});

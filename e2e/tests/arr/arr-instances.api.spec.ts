import { test, expect, TEST_CONFIG } from '../fixtures/base';
import type { ArrType } from '../helpers/api/arr';
import { ArrStubs } from '../helpers/mocks';

const ARR_TYPES: ArrType[] = ['sonarr', 'radarr', 'lidarr', 'readarr', 'whisparr'];

test.describe('Arr — instance CRUD', () => {
  for (const type of ARR_TYPES) {
    test(`${type}: create + list + update + delete instance`, async ({ api, mocks }) => {
      await mocks.arr.stub(ArrStubs.arrHealthStub({ apiKey: 'e2e-test-key' }));

      const create = await api.arr.createInstance(type, {
        name: `${type}-e2e`,
        url: TEST_CONFIG.mocks.arrUrl,
        apiKey: 'e2e-test-key',
        version: 3,
        enabled: true,
      });
      expect(create.status).toBeLessThan(300);
      const created = await create.json();
      expect(created.id).toBeTruthy();
      expect(created.name).toBe(`${type}-e2e`);

      const listed = await (await api.arr.getConfig(type)).json();
      expect(listed.instances.some((i: { id: string }) => i.id === created.id)).toBe(true);

      const update = await api.arr.updateInstance(type, created.id, {
        name: `${type}-renamed`,
        url: TEST_CONFIG.mocks.arrUrl,
        apiKey: 'e2e-test-key',
        version: 3,
        enabled: false,
      });
      expect(update.ok).toBe(true);
      const updated = await update.json();
      expect(updated.name).toBe(`${type}-renamed`);
      expect(updated.enabled).toBe(false);

      const del = await api.arr.deleteInstance(type, created.id);
      expect(del.status).toBe(204);

      const after = await (await api.arr.getConfig(type)).json();
      expect(after.instances.some((i: { id: string }) => i.id === created.id)).toBe(false);
    });

    test(`${type}: rejects instance with missing apiKey`, async ({ api }) => {
      const res = await api.arr.createInstance(type, {
        name: `${type}-bad`,
        url: TEST_CONFIG.mocks.arrUrl,
        apiKey: '',
        version: 3,
      });
      expect(res.status).toBeGreaterThanOrEqual(400);
      expect(res.status).toBeLessThan(500);
    });

    test(`${type}: rejects instance with malformed url`, async ({ api }) => {
      const res = await api.arr.createInstance(type, {
        name: `${type}-bad-url`,
        url: 'not-a-url',
        apiKey: 'k',
        version: 3,
      });
      expect(res.status).toBeGreaterThanOrEqual(400);
      expect(res.status).toBeLessThan(500);
    });
  }
});

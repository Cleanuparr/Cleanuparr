import { test, expect } from '../fixtures/base';
import type { ArrType } from '../helpers/api/arr';

const ARR_TYPES: ArrType[] = ['sonarr', 'radarr', 'lidarr', 'readarr', 'whisparr'];

test.describe('Arr — base config CRUD', () => {
  for (const type of ARR_TYPES) {
    test(`GET /api/configuration/${type} returns config + instances`, async ({ api }) => {
      const res = await api.arr.getConfig(type);
      expect(res.status).toBe(200);
      const body = await res.json();
      expect(Array.isArray(body.instances)).toBe(true);
    });

    test(`PUT /api/configuration/${type} accepts valid config`, async ({ api }) => {
      const current = await (await api.arr.getConfig(type)).json();
      const res = await api.arr.updateConfig(type, {
        ...current,
        failedImportMaxStrikes: 3,
      });
      expect(res.status).toBe(200);
    });

    test(`GET /api/configuration/${type} requires auth`, async ({ anonymousApi }) => {
      const res = await anonymousApi.arr.getConfig(type);
      expect(res.status).toBe(401);
    });
  }
});

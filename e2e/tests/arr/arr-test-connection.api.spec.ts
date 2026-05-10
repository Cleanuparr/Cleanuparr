import { test, expect, TEST_CONFIG } from '../fixtures/base';
import type { ArrType } from '../helpers/api/arr';
import { ArrStubs } from '../helpers/mocks';

const ARR_TYPES: ArrType[] = ['sonarr', 'radarr', 'lidarr', 'readarr', 'whisparr'];

test.describe('Arr — test connection', () => {
  for (const type of ARR_TYPES) {
    test(`${type}: returns success when WireMock returns 200`, async ({ api, mocks }) => {
      await mocks.arr.stub(ArrStubs.arrHealthStub({ apiKey: 'good-key' }));
      const res = await api.arr.testInstance(type, {
        name: `${type}-conn`,
        url: TEST_CONFIG.mocks.arrUrl,
        apiKey: 'good-key',
        version: 3,
      });
      expect(res.ok).toBe(true);
    });

    test(`${type}: returns failure when WireMock returns 401`, async ({ api, mocks }) => {
      await mocks.arr.stub(ArrStubs.arrUnauthorizedStub('/api/v3/system/status'));
      const res = await api.arr.testInstance(type, {
        name: `${type}-conn-bad`,
        url: TEST_CONFIG.mocks.arrUrl,
        apiKey: 'bad-key',
        version: 3,
      });
      expect(res.ok).toBe(false);
    });

    test(`${type}: returns failure when host unreachable`, async ({ api }) => {
      const res = await api.arr.testInstance(type, {
        name: `${type}-conn-down`,
        url: 'http://127.0.0.1:1',
        apiKey: 'k',
        version: 3,
      });
      expect(res.ok).toBe(false);
    });
  }
});

import { test, expect } from '../fixtures/base';

test.describe('General — strikes purge', () => {
  test('POST /strikes/purge returns deletion counts', async ({ api }) => {
    const res = await api.general.purgeStrikes();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('deletedStrikes');
    expect(body).toHaveProperty('deletedItems');
    expect(typeof body.deletedStrikes).toBe('number');
    expect(typeof body.deletedItems).toBe('number');
  });

  test('after purge, /api/strikes returns no items', async ({ api }) => {
    await api.general.purgeStrikes();
    const list = await (await api.strikes.list()).json();
    const items = list.items ?? list;
    expect(Array.isArray(items) ? items.length : items.totalItems ?? 0).toBe(
      Array.isArray(items) ? 0 : 0,
    );
  });

  test('POST requires auth', async ({ anonymousApi }) => {
    const res = await anonymousApi.general.purgeStrikes();
    expect(res.status).toBe(401);
  });
});

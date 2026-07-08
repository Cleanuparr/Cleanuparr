import { test, expect, TEST_CONFIG } from '../fixtures/base';
import type { DownloadClientType } from '../helpers/api/download-client';
import { buildDownloadClientPayload } from '../helpers/api/download-client';

const TYPES: DownloadClientType[] = ['qbittorrent', 'transmission', 'deluge', 'utorrent', 'rtorrent'];

test.describe('DownloadClient — CRUD', () => {
  for (const type of TYPES) {
    test(`${type}: create + list + update + delete`, async ({ api }) => {
      const payload = buildDownloadClientPayload(type, {
        name: `${type}-e2e`,
        host: TEST_CONFIG.mocks.downloadClientUrl,
        username: 'admin',
        password: 'admin',
      });

      const create = await api.downloadClient.create(payload);
      if (!create.ok) {
        console.error(`${type} create failed:`, create.status, await create.text());
      }
      expect(create.status).toBeLessThan(300);
      const created = await create.json();
      expect(created.id).toBeTruthy();
      expect(created.typeName.toLowerCase()).toBe(payload.typeName?.toLowerCase());

      const list = await (await api.downloadClient.list()).json();
      const clients = list.clients ?? list;
      expect(clients.some((c: { id: string }) => c.id === created.id)).toBe(true);

      const update = await api.downloadClient.update(created.id, {
        ...payload,
        name: `${type}-renamed`,
        enabled: false,
      });
      expect(update.ok).toBe(true);

      const del = await api.downloadClient.delete(created.id);
      expect(del.status).toBe(204);
    });
  }

  test('GET requires auth', async ({ anonymousApi }) => {
    const res = await anonymousApi.downloadClient.list();
    expect(res.status).toBe(401);
  });
});

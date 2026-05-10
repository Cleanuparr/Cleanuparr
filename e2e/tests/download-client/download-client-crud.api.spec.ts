import { test, expect, TEST_CONFIG } from '../fixtures/base';
import type { DownloadClientType } from '../helpers/api/download-client';

const TYPES: DownloadClientType[] = ['qbittorrent', 'transmission', 'deluge', 'utorrent', 'rtorrent'];

test.describe('DownloadClient — CRUD', () => {
  for (const type of TYPES) {
    test(`${type}: create + list + update + delete`, async ({ api }) => {
      const create = await api.downloadClient.create({
        name: `${type}-e2e`,
        type,
        host: TEST_CONFIG.mocks.downloadClientUrl.replace(/^https?:\/\//, '').split(':')[0],
        port: 9200,
        username: 'admin',
        password: 'admin',
        useSsl: false,
        enabled: true,
      });
      expect(create.status).toBeLessThan(300);
      const created = await create.json();
      expect(created.id).toBeTruthy();
      expect(created.type).toBe(type);

      const list = await (await api.downloadClient.list()).json();
      const clients = list.clients ?? list;
      expect(clients.some((c: { id: string }) => c.id === created.id)).toBe(true);

      const update = await api.downloadClient.update(created.id, {
        name: `${type}-renamed`,
        type,
        host: 'localhost',
        port: 9200,
        username: 'admin',
        password: 'admin',
        useSsl: false,
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

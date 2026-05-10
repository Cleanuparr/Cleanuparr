import { test, expect, TEST_CONFIG } from '../fixtures/base';

test.describe('DownloadCleaner — unlinked config', () => {
  test('GET + PUT round-trip for a download client', async ({ api }) => {
    const created = await (
      await api.downloadClient.create({
        name: 'qb-unlinked',
        type: 'qbittorrent',
        host: 'localhost',
        port: 9200,
        username: 'admin',
        password: 'admin',
        useSsl: false,
        enabled: true,
      })
    ).json();
    expect(created.id).toBeTruthy();

    const initial = await api.downloadCleaner.getUnlinkedConfig(created.id);
    expect(initial.status).toBe(200);

    const update = await api.downloadCleaner.updateUnlinkedConfig(created.id, {
      enabled: true,
      categories: ['radarr', 'sonarr'],
      ignoredRootDirectories: ['/downloads/manual'],
      useTag: true,
      tag: 'cleanuparr-unlinked',
    });
    expect(update.ok).toBe(true);

    const after = await (await api.downloadCleaner.getUnlinkedConfig(created.id)).json();
    expect(after.enabled).toBe(true);
    expect(after.categories).toEqual(expect.arrayContaining(['radarr', 'sonarr']));
    expect(after.tag).toBe('cleanuparr-unlinked');
  });

  test('GET returns 404 for unknown client', async ({ api }) => {
    const res = await api.downloadCleaner.getUnlinkedConfig('00000000-0000-0000-0000-000000000000');
    expect([404, 400]).toContain(res.status);
  });
});

import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { buildDownloadClientPayload } from '../helpers/api/download-client';

test.describe('DownloadCleaner — unlinked config', () => {
  test('GET + PUT round-trip for a download client', async ({ api }) => {
    const created = await (
      await api.downloadClient.create(
        buildDownloadClientPayload('qbittorrent', {
          name: 'qb-unlinked',
          host: TEST_CONFIG.mocks.downloadClientUrl,
          username: 'admin',
          password: 'admin',
        }),
      )
    ).json();
    expect(created.id).toBeTruthy();

    const initial = await api.downloadCleaner.getUnlinkedConfig(created.id);
    // Backend returns 204 when no unlinked config row exists for the client.
    expect([200, 204]).toContain(initial.status);

    const update = await api.downloadCleaner.updateUnlinkedConfig(created.id, {
      enabled: true,
      categories: ['radarr', 'sonarr'],
      ignoredRootDirs: ['/downloads/manual'],
      useTag: true,
      targetCategory: 'cleanuparr-unlinked',
    });
    expect(update.ok).toBe(true);

    const after = await (await api.downloadCleaner.getUnlinkedConfig(created.id)).json();
    expect(after.enabled).toBe(true);
    expect(after.categories).toEqual(expect.arrayContaining(['radarr', 'sonarr']));
    expect(after.targetCategory).toBe('cleanuparr-unlinked');
  });

  test('GET returns 404 for unknown client', async ({ api }) => {
    const res = await api.downloadCleaner.getUnlinkedConfig('00000000-0000-0000-0000-000000000000');
    expect([404, 400]).toContain(res.status);
  });
});

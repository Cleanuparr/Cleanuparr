import { test, expect } from '@playwright/test';
import { join, resolve } from 'node:path';
import {
  loginAndGetToken,
  createDownloadClient,
  listDownloadClients,
  deleteDownloadClient,
  triggerJob,
} from '../helpers/app-api';
import { CleanuparrApi } from '../helpers/api';
import { QBittorrentDriver } from '../helpers/torrent-clients/qbittorrent';
import { mkdirShared, writeFileShared } from '../helpers/shared-volume';

const HOST_DOWNLOADS = resolve(__dirname, '..', '..', 'test-data', 'downloads');
const BLACKLIST_REL_PATH = 'blacklist-sync/blacklist.txt';
const APP_BLACKLIST_PATH = `/e2e-downloads/${BLACKLIST_REL_PATH}`;
const POLL_TIMEOUT_MS = 30_000;

test.describe.serial('BlacklistSync: qBittorrent excluded file names', () => {
  const driver = new QBittorrentDriver();
  let token: string;
  let clientId: string | undefined;
  let original: Record<string, unknown>;

  test.beforeAll(async () => {
    token = await loginAndGetToken();
    original = await (await new CleanuparrApi({ token }).blacklistSync.getConfig()).json();

    for (const client of await listDownloadClients(token)) {
      await deleteDownloadClient(token, client.id);
    }
    mkdirShared(join(HOST_DOWNLOADS, 'blacklist-sync'));
    await driver.ready();
  });

  test.afterAll(async () => {
    if (clientId) {
      await deleteDownloadClient(token, clientId);
    }
    await new CleanuparrApi({ token }).blacklistSync.updateConfig(original);
    await driver.setExcludedFileNames('');
    writeFileShared(join(HOST_DOWNLOADS, BLACKLIST_REL_PATH), '');
  });

  test('a sync run writes the blacklist patterns into qBittorrent', async () => {
    test.setTimeout(90_000);

    // The sync skips a client already synced for the same content hash.
    const unique = `*.e2e${Date.now().toString(36)}`;
    writeFileShared(join(HOST_DOWNLOADS, BLACKLIST_REL_PATH), `*.exe\n${unique}\n`);
    await driver.setExcludedFileNames('');

    const createRes = await createDownloadClient(token, {
      enabled: true,
      name: 'qBittorrent blacklist-sync e2e',
      typeName: driver.typeName,
      host: driver.cleanuparrHost,
      username: driver.username ?? '',
      password: driver.password ?? '',
    });
    expect(createRes.status, `createDownloadClient: ${createRes.status}`).toBeLessThan(300);
    clientId = (await createRes.json()).id;

    const updateRes = await new CleanuparrApi({ token }).blacklistSync.updateConfig({
      ...original,
      enabled: true,
      blacklistPath: APP_BLACKLIST_PATH,
    });
    expect(updateRes.ok, `updateConfig: ${updateRes.status}`).toBe(true);

    const triggerRes = await triggerJob(token, 'BlacklistSynchronizer');
    expect(triggerRes.ok, `triggerJob: ${triggerRes.status}`).toBe(true);

    await expect
      .poll(() => driver.getExcludedFileNames(), { timeout: POLL_TIMEOUT_MS })
      .toBe(`*.exe\n${unique}`);
  });
});

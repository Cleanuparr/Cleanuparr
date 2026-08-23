import { test, expect } from '@playwright/test';
import { existsSync, readdirSync } from 'node:fs';
import { join, resolve } from 'node:path';
import {
  loginAndGetToken,
  createDownloadClient,
  listDownloadClients,
  deleteDownloadClient,
  updateDownloadCleanerConfig,
  getDownloadCleanerConfig,
  updateOrphanedFilesConfig,
  triggerJob,
} from '../helpers/app-api';
import { resetDirectory } from '../helpers/torrent-fixtures';
import { mkdirShared, writeFileShared } from '../helpers/shared-volume';

/**
 * Regression guard for the orphaned-files cleanup safety bail.
 *
 * The cleaner moves and purges nothing for a download client whose torrent
 * list it could not retrieve. Drop that guard and an erroring client makes
 * every scan-directory entry look unclaimed, so the cleaner moves real
 * downloads out.
 *
 * This spec points the client at a port nothing listens on. That hits the
 * catch path in `TryAddClaimedPathsAsync`, or the LoginAsync skip above it.
 * Both produce the same outcome for the user.
 *
 * A reachable client reporting 0 torrents is the opposite case: it claims
 * nothing, so the cleaner does scan its directories. See
 * `orphaned-files-empty-client.spec.ts`.
 */

const HOST_DOWNLOADS = resolve(__dirname, '..', '..', 'test-data', 'downloads');
const APP_DOWNLOADS = '/e2e-downloads';
const SLUG = 'qbittorrent-unreachable';
const HOST_SCAN_DIR = join(HOST_DOWNLOADS, SLUG);
const HOST_ORPHANED_DIR = join(HOST_DOWNLOADS, SLUG, 'orphaned');
const APP_SCAN_DIR = `${APP_DOWNLOADS}/${SLUG}`;
const APP_ORPHANED_DIR = `${APP_DOWNLOADS}/${SLUG}/orphaned`;

function writeFile(dir: string, name: string, content = 'real-download'): string {
  mkdirShared(dir);
  const path = join(dir, name);
  writeFileShared(path, content);
  return path;
}

async function triggerAndSettle(token: string): Promise<void> {
  const res = await triggerJob(token, 'DownloadCleaner');
  expect(res.ok, `triggerJob: ${res.status}`).toBe(true);
  // No seeding downloads to wait on -> only the cleaner's own bookkeeping +
  // filesystem walk. 3s is the same window used by orphaned-files-behaviors.
  await new Promise((r) => setTimeout(r, 3000));
}

test.describe.serial('Orphaned files cleanup — refuses to scan when client data is untrusted', () => {
  let token: string;

  test.beforeAll(async () => {
    token = await loginAndGetToken();

    const existing = await listDownloadClients(token);
    for (const client of existing) {
      await deleteDownloadClient(token, client.id);
    }

    const dcCurrent = await (await getDownloadCleanerConfig(token)).json();
    await updateDownloadCleanerConfig(token, {
      enabled: true,
      cronExpression: dcCurrent.cronExpression || '0 0 * * * ?',
      useAdvancedScheduling: dcCurrent.useAdvancedScheduling ?? false,
      ignoredDownloads: [],
    });

    mkdirShared(HOST_DOWNLOADS);
  });

  test.beforeEach(async () => {
    // A fresh scan dir holding one fake real download.
    // A scan that runs anyway lands it in HOST_ORPHANED_DIR.
    resetDirectory(HOST_SCAN_DIR);
    mkdirShared(HOST_ORPHANED_DIR);

    // Each test registers its own client; remove anything stale.
    const existing = await listDownloadClients(token);
    for (const client of existing) {
      await deleteDownloadClient(token, client.id);
    }
  });

  test('Unreachable download client → files in the scan dir are not moved', async () => {
    test.setTimeout(60_000);

    const realDownload = writeFile(HOST_SCAN_DIR, 'real-download.mkv');

    const createRes = await createDownloadClient(token, {
      enabled: true,
      name: 'qBittorrent unreachable',
      typeName: 'qBittorrent',
      type: 'Torrent',
      // Nothing listens on port 1, so the qBit client fails to connect.
      host: 'http://127.0.0.1:1',
      username: 'admin',
      password: 'adminadmin',
      downloadDirectorySource: '/downloads',
      downloadDirectoryTarget: APP_SCAN_DIR,
    });
    expect(createRes.ok, `createDownloadClient: ${createRes.status}`).toBe(true);
    const created = await createRes.json();

    const ofcRes = await updateOrphanedFilesConfig(token, created.id, {
      enabled: true,
      scanDirectories: [APP_SCAN_DIR],
      orphanedDirectory: APP_ORPHANED_DIR,
      minFileAgeHours: 0,
    });
    expect(ofcRes.ok, `updateOrphanedFilesConfig: ${ofcRes.status}`).toBe(true);

    await triggerAndSettle(token);

    expect(existsSync(realDownload)).toBe(true);
    expect(existsSync(join(HOST_ORPHANED_DIR, 'real-download.mkv'))).toBe(false);
    expect(readdirSync(HOST_ORPHANED_DIR).length).toBe(0);
  });
});

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
import { WireMockClient, DownloadClientStubs } from '../helpers/mocks';
import { TEST_CONFIG } from '../helpers/test-config';
import { QBittorrentDriver } from '../helpers/torrent-clients/qbittorrent';
import { resetDirectory } from '../helpers/torrent-fixtures';
import { mkdirShared, writeFileShared } from '../helpers/shared-volume';

/**
 * Regression guard for a class of bugs where SABnzbd answers with a broken
 * response and the cleaner reads it as an empty/idle client instead of an
 * unreachable one:
 *  - a `{}` history body (version drift, a proxy rewrite) used to read as
 *    zero jobs, so the orphan scan moved every completed job folder;
 *  - a busy queue whose `get_config` call fails or returns an unusable
 *    `download_dir` used to claim nothing from the incomplete dir instead of
 *    failing the client, so the scan could move an in-progress download.
 *
 * All three cases are WireMock-backed: there is no live-container SAB leg for
 * "SAB answers with garbage", so these responses have to be stubbed.
 */

const HOST_DOWNLOADS = resolve(__dirname, '..', '..', 'test-data', 'downloads');
const SLUG = 'sab-broken-response';
const HOST_SCAN_DIR = join(HOST_DOWNLOADS, SLUG);
const HOST_ORPHANED_DIR = join(HOST_DOWNLOADS, SLUG, 'orphaned');
const APP_SCAN_DIR = `/e2e-downloads/${SLUG}`;
const APP_ORPHANED_DIR = `/e2e-downloads/${SLUG}/orphaned`;

const SIBLING_SLUG = 'sab-broken-response-qbit';
const HOST_SIBLING_SCAN_DIR = join(HOST_DOWNLOADS, SIBLING_SLUG);
const HOST_SIBLING_ORPHANED_DIR = join(HOST_DOWNLOADS, SIBLING_SLUG, 'orphaned');
const APP_SIBLING_SCAN_DIR = `/e2e-downloads/${SIBLING_SLUG}`;
const APP_SIBLING_ORPHANED_DIR = `/e2e-downloads/${SIBLING_SLUG}/orphaned`;

function writeOrphanFile(dir: string, name: string): string {
  mkdirShared(dir);
  const path = join(dir, name);
  writeFileShared(path, 'orphan');
  return path;
}

async function triggerAndSettle(token: string): Promise<void> {
  const res = await triggerJob(token, 'DownloadCleaner');
  expect(res.ok, `triggerJob: ${res.status}`).toBe(true);
  // The cleaner walks the directories on a worker thread; give it a window
  // before asserting either outcome.
  await new Promise((r) => setTimeout(r, 4_000));
}

test.describe.serial('Orphaned files cleanup: SABnzbd broken responses', () => {
  const downloadClientMock = new WireMockClient(TEST_CONFIG.mocks.downloadClientAdminUrl);
  let token: string;
  let clientId: string;

  test.beforeAll(async () => {
    test.setTimeout(60_000);
    token = await loginAndGetToken();
    await downloadClientMock.waitReady();

    for (const client of await listDownloadClients(token)) {
      await deleteDownloadClient(token, client.id);
    }

    const createRes = await createDownloadClient(token, {
      enabled: true,
      name: 'SABnzbd broken-response e2e',
      type: 'Usenet',
      typeName: 'Sabnzbd',
      host: TEST_CONFIG.mocks.downloadClientUrl,
      apiKey: 'e2e-key',
      downloadDirectorySource: '/downloads',
      downloadDirectoryTarget: APP_SCAN_DIR,
    });
    expect(createRes.status, `createDownloadClient: ${createRes.status}`).toBeLessThan(300);
    clientId = (await createRes.json()).id;

    const dcCurrent = await (await getDownloadCleanerConfig(token)).json();
    await updateDownloadCleanerConfig(token, {
      enabled: true,
      cronExpression: dcCurrent.cronExpression || '0 0 * * * ?',
      useAdvancedScheduling: dcCurrent.useAdvancedScheduling ?? false,
      ignoredDownloads: [],
    });

    const ofc = await updateOrphanedFilesConfig(token, clientId, {
      enabled: true,
      scanDirectories: [APP_SCAN_DIR],
      orphanedDirectory: APP_ORPHANED_DIR,
      excludePatterns: [],
      minFileAgeHours: 0,
      purgeAfterHours: null,
    });
    expect(ofc.status).toBe(200);
  });

  test.afterAll(async () => {
    if (clientId) {
      await deleteDownloadClient(token, clientId).catch(() => undefined);
    }
  });

  test.beforeEach(async () => {
    resetDirectory(HOST_SCAN_DIR);
    mkdirShared(HOST_ORPHANED_DIR);
    await downloadClientMock.resetAll();
  });

  test('a {} history body leaves the scan dir untouched; a valid empty history still moves it', async () => {
    const orphan = writeOrphanFile(HOST_SCAN_DIR, 'orphan.mkv');

    await downloadClientMock.stub(DownloadClientStubs.sabQueueStub([]));
    await downloadClientMock.stub(DownloadClientStubs.sabEmptyBodyStub('history'));

    await triggerAndSettle(token);

    expect(existsSync(orphan), 'a broken history body must not be read as an empty client').toBe(true);
    expect(readdirSync(HOST_ORPHANED_DIR)).toHaveLength(0);

    // Positive control: a valid empty history still proves the scan actually ran.
    await downloadClientMock.resetAll();
    await downloadClientMock.stub(DownloadClientStubs.sabQueueStub([]));
    await downloadClientMock.stub(DownloadClientStubs.sabHistoryStub([]));

    await triggerAndSettle(token);

    expect(existsSync(orphan)).toBe(false);
    expect(readdirSync(HOST_ORPHANED_DIR)).toContain('orphan.mkv');
  });

  test('a busy queue with a relative download_dir leaves the scan dir untouched', async () => {
    const orphan = writeOrphanFile(HOST_SCAN_DIR, 'orphan.mkv');

    await downloadClientMock.stub(DownloadClientStubs.sabQueueStub([{ nzo_id: 'SAB-BUSY-1', status: 'Downloading' }]));
    await downloadClientMock.stub(DownloadClientStubs.sabHistoryStub([]));
    await downloadClientMock.stub(DownloadClientStubs.sabGetConfigStub('Downloads/incomplete'));

    await triggerAndSettle(token);

    expect(existsSync(orphan), 'a relative download_dir must fail the client, not claim nothing').toBe(true);
    expect(readdirSync(HOST_ORPHANED_DIR)).toHaveLength(0);
  });

  test('a busy queue with a failing get_config leaves the SAB dir untouched while a sibling client is still cleaned', async () => {
    resetDirectory(HOST_SIBLING_SCAN_DIR);
    mkdirShared(HOST_SIBLING_ORPHANED_DIR);

    const sabOrphan = writeOrphanFile(HOST_SCAN_DIR, 'orphan.mkv');
    const siblingOrphan = writeOrphanFile(HOST_SIBLING_SCAN_DIR, 'orphan.mkv');

    await downloadClientMock.stub(DownloadClientStubs.sabQueueStub([{ nzo_id: 'SAB-BUSY-2', status: 'Downloading' }]));
    await downloadClientMock.stub(DownloadClientStubs.sabHistoryStub([]));
    await downloadClientMock.stub(DownloadClientStubs.sabGetConfigErrorStub());

    const qbitDriver = new QBittorrentDriver();
    await qbitDriver.ready();
    await qbitDriver.clearAllTorrents();

    const siblingRes = await createDownloadClient(token, {
      enabled: true,
      name: 'qBittorrent sibling for sab-broken-response',
      typeName: qbitDriver.typeName,
      type: 'Torrent',
      host: qbitDriver.cleanuparrHost,
      username: qbitDriver.username ?? '',
      password: qbitDriver.password ?? '',
      downloadDirectorySource: '/downloads',
      downloadDirectoryTarget: APP_SIBLING_SCAN_DIR,
    });
    expect(siblingRes.status).toBeLessThan(300);
    const sibling = await siblingRes.json();

    try {
      const siblingOfc = await updateOrphanedFilesConfig(token, sibling.id, {
        enabled: true,
        scanDirectories: [APP_SIBLING_SCAN_DIR],
        orphanedDirectory: APP_SIBLING_ORPHANED_DIR,
        excludePatterns: [],
        minFileAgeHours: 0,
        purgeAfterHours: null,
      });
      expect(siblingOfc.status).toBe(200);

      await triggerAndSettle(token);

      expect(existsSync(sabOrphan), 'a failing get_config must fail the SAB client, not claim nothing').toBe(true);
      expect(readdirSync(HOST_ORPHANED_DIR)).toHaveLength(0);

      expect(existsSync(siblingOrphan), 'a sibling client must still be scanned when SAB fails').toBe(false);
      expect(readdirSync(HOST_SIBLING_ORPHANED_DIR)).toContain('orphan.mkv');
    } finally {
      await deleteDownloadClient(token, sibling.id).catch(() => undefined);
    }
  });
});

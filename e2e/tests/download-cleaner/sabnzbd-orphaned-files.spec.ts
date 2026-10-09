import { test, expect } from '@playwright/test';
import { existsSync, mkdirSync, readdirSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import {
  loginAndGetToken,
  createDownloadClient,
  deleteDownloadClient,
  listDownloadClients,
  getDownloadCleanerConfig,
  updateDownloadCleanerConfig,
  updateOrphanedFilesConfig,
  triggerJob,
} from '../helpers/app-api';
import { SabnzbdDriver } from '../helpers/usenet-clients/sabnzbd';
import { mkdirShared } from '../helpers/shared-volume';

/**
 * SABnzbd only has the OrphanClaims capability for DownloadCleaner: no seeding
 * rules, unlinked downloads or dead-torrent detection (usenet jobs are moved by
 * the arr on import, never hardlinked, so "unlinked" is meaningless for them;
 * `DownloadClientCapabilityConsistencyTests` on the backend pins this). This spec
 * covers the one capability it does have: claiming its own paths so orphaned-files
 * cleanup doesn't delete a job it's still using.
 */

const HOST_DOWNLOADS = join(__dirname, '..', '..', 'test-data', 'downloads', 'sabnzbd');
const APP_DOWNLOADS = '/e2e-downloads/sabnzbd';

function resetDir(path: string): void {
  rmSync(path, { recursive: true, force: true });
  mkdirShared(path);
}

async function waitForMove(dir: string, name: string, timeoutMs = 30_000): Promise<boolean> {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (existsSync(join(dir, name))) {
      return true;
    }
    await new Promise((r) => setTimeout(r, 1000));
  }
  return false;
}

test.describe.serial('SABnzbd live: orphaned files claims', () => {
  const driver = new SabnzbdDriver();
  let token: string;
  let clientId: string;

  test.beforeAll(async () => {
    test.setTimeout(60_000);
    token = await loginAndGetToken();
    await driver.ready();
    await driver.clearAll();
    await driver.resumeQueue();

    for (const client of await listDownloadClients(token)) {
      await deleteDownloadClient(token, client.id);
    }

    const createRes = await createDownloadClient(token, {
      enabled: true,
      name: 'SABnzbd orphaned-files e2e',
      typeName: 'Sabnzbd',
      host: driver.cleanuparrHost,
      apiKey: driver.apiKey,
      downloadDirectorySource: '/downloads',
      downloadDirectoryTarget: APP_DOWNLOADS,
    });
    expect(createRes.status, `createDownloadClient: ${createRes.status}`).toBeLessThan(300);
    clientId = (await createRes.json()).id;

    const dc = await (await getDownloadCleanerConfig(token)).json();
    await updateDownloadCleanerConfig(token, {
      enabled: true,
      cronExpression: dc.cronExpression || '0 0 * * * ?',
      useAdvancedScheduling: dc.useAdvancedScheduling ?? false,
      ignoredDownloads: [],
    });
  });

  test.afterAll(async () => {
    if (clientId) {
      await deleteDownloadClient(token, clientId).catch(() => undefined);
    }
    await driver.clearAll().catch(() => undefined);
    await driver.resumeQueue().catch(() => undefined);
  });

  test('a completed job storage folder survives; an unrelated folder is moved out', async () => {
    test.setTimeout(90_000);
    const completeDir = join(HOST_DOWNLOADS, 'complete');
    resetDir(completeDir);
    const orphanedDir = join(HOST_DOWNLOADS, 'complete-orphaned');
    resetDir(orphanedDir);

    const ofc = await updateOrphanedFilesConfig(token, clientId, {
      enabled: true,
      scanDirectories: [`${APP_DOWNLOADS}/complete`],
      orphanedDirectory: `${APP_DOWNLOADS}/complete-orphaned`,
      minFileAgeHours: 0,
    });
    expect(ofc.status).toBe(200);

    const dirName = `claim-${Date.now().toString(36)}`;
    // Two files, not one: SABnzbd reports a single-file job's `storage` as the file
    // path itself rather than its folder, which GetClaimedPathsAsync does not expect.
    driver.writeArticleFile(dirName, 'data1.bin', 32_768);
    driver.writeArticleFile(dirName, 'data2.bin', 32_768);
    const nzoId = await driver.addWorkingNzbDir(dirName, 'e2e-ofc');
    await driver.waitForHistoryStatus(nzoId, 'Completed', 60_000);

    // A loose folder with no SABnzbd job behind it at all.
    mkdirSync(join(completeDir, 'leftover-unrelated'), { recursive: true });

    const trig = await triggerJob(token, 'DownloadCleaner');
    expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);

    expect(await waitForMove(orphanedDir, 'leftover-unrelated'), 'unrelated folder should have been moved out').toBe(true);
    expect(existsSync(join(completeDir, dirName)), 'the completed job folder must survive: it is claimed').toBe(true);
  });

  test('a single-file job survives even though SABnzbd reports storage as the file, not its folder', async () => {
    test.setTimeout(90_000);
    const completeDir = join(HOST_DOWNLOADS, 'complete');
    resetDir(completeDir);
    const orphanedDir = join(HOST_DOWNLOADS, 'complete-orphaned');
    resetDir(orphanedDir);

    const ofc = await updateOrphanedFilesConfig(token, clientId, {
      enabled: true,
      scanDirectories: [`${APP_DOWNLOADS}/complete`],
      orphanedDirectory: `${APP_DOWNLOADS}/complete-orphaned`,
      minFileAgeHours: 0,
    });
    expect(ofc.status).toBe(200);

    const fileName = `single-${Date.now().toString(36)}.bin`;
    const nzoId = await driver.addWorkingNzb(fileName, 32_768, 'e2e-ofc');
    const history = await driver.waitForHistoryStatus(nzoId, 'Completed', 60_000);

    // SABnzbd's storage for a single-file job names the file inside its job folder,
    // not the folder itself: the bug GetClaimedPathsAsync must work around.
    expect(history.storage, 'the live quirk this test guards against').toMatch(/\.bin$/);

    mkdirSync(join(completeDir, 'leftover-unrelated'), { recursive: true });

    const trig = await triggerJob(token, 'DownloadCleaner');
    expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);

    expect(await waitForMove(orphanedDir, 'leftover-unrelated'), 'unrelated folder should have been moved out').toBe(true);
    expect(existsSync(join(completeDir, history.name)), 'the completed job folder must survive: it is claimed').toBe(true);
  });

  test('a busy queue protects every top-level entry under the incomplete dir', async () => {
    test.setTimeout(90_000);
    const incompleteDir = join(HOST_DOWNLOADS, 'incomplete');
    resetDir(incompleteDir);
    const orphanedDir = join(HOST_DOWNLOADS, 'incomplete-orphaned');
    resetDir(orphanedDir);
    const sentinelDir = join(HOST_DOWNLOADS, 'busy-sentinel');
    resetDir(sentinelDir);

    const ofc = await updateOrphanedFilesConfig(token, clientId, {
      enabled: true,
      scanDirectories: [`${APP_DOWNLOADS}/incomplete`, `${APP_DOWNLOADS}/busy-sentinel`],
      orphanedDirectory: `${APP_DOWNLOADS}/incomplete-orphaned`,
      minFileAgeHours: 0,
    });
    expect(ofc.status).toBe(200);

    // A leftover with no live job, placed directly under the incomplete dir.
    mkdirSync(join(incompleteDir, 'leftover-incomplete'), { recursive: true });
    mkdirSync(join(sentinelDir, 'leftover-unrelated'), { recursive: true });

    // Pausing the whole queue before adding keeps one job permanently non-history,
    // which is what makes GetClaimedPathsAsync treat the queue as "busy".
    let nzoId: string | undefined;

    try {
      await driver.pauseQueue();
      nzoId = await driver.addWorkingNzb('busy-queue.bin', 300_000, 'e2e-ofc');
      const trig = await triggerJob(token, 'DownloadCleaner');
      expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);

      expect(await waitForMove(orphanedDir, 'leftover-unrelated'), 'a sentinel outside the incomplete dir proves the scan ran').toBe(true);
      expect(
        existsSync(join(incompleteDir, 'leftover-incomplete')),
        'a busy queue must claim every top-level entry defensively, including an unrelated one',
      ).toBe(true);
    } finally {
      if (nzoId) {
        await driver.deleteQueueJob(nzoId, true).catch(() => undefined);
      }
      await driver.resumeQueue();
    }

    // Queue is empty again (not busy): the same leftover is now a real orphan.
    const trig2 = await triggerJob(token, 'DownloadCleaner');
    expect(trig2.ok, `triggerJob: ${trig2.status}`).toBe(true);
    expect(await waitForMove(orphanedDir, 'leftover-incomplete'), 'leftover should be cleaned once the queue is idle').toBe(true);
  });

  test('a job in a category subfolder survives when that subfolder is the scan dir', async () => {
    test.setTimeout(90_000);
    const category = 'e2e-ofc-nested';
    const nestedDir = join(HOST_DOWNLOADS, 'complete', 'nested');
    resetDir(nestedDir);
    const orphanedDir = join(HOST_DOWNLOADS, 'complete-orphaned');
    resetDir(orphanedDir);

    await driver.setCategoryDir(category, 'nested');

    const ofc = await updateOrphanedFilesConfig(token, clientId, {
      enabled: true,
      scanDirectories: [`${APP_DOWNLOADS}/complete/nested`],
      orphanedDirectory: `${APP_DOWNLOADS}/complete-orphaned`,
      minFileAgeHours: 0,
    });
    expect(ofc.status).toBe(200);

    const dirName = `claim-nested-${Date.now().toString(36)}`;
    driver.writeArticleFile(dirName, 'data1.bin', 32_768);
    driver.writeArticleFile(dirName, 'data2.bin', 32_768);
    const nzoId = await driver.addWorkingNzbDir(dirName, category);
    await driver.waitForHistoryStatus(nzoId, 'Completed', 60_000);

    // A loose folder with no SABnzbd job behind it, sitting next to the job folder.
    mkdirSync(join(nestedDir, 'leftover-unrelated'), { recursive: true });

    try {
      const trig = await triggerJob(token, 'DownloadCleaner');
      expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);

      expect(await waitForMove(orphanedDir, 'leftover-unrelated'), 'unrelated folder should have been moved out').toBe(true);
      expect(existsSync(join(nestedDir, dirName)), 'the completed job folder in the category subfolder is claimed and must survive').toBe(true);
    } finally {
      await driver.deleteCategory(category);
    }
  });

  test('a job in a category with an absolute folder outside complete_dir survives', async () => {
    test.setTimeout(90_000);
    const category = 'e2e-ofc-abs';
    const absDir = join(HOST_DOWNLOADS, 'e2e-abs');
    resetDir(absDir);
    const orphanedDir = join(HOST_DOWNLOADS, 'complete-orphaned');
    resetDir(orphanedDir);

    await driver.setCategoryDir(category, '/downloads/e2e-abs');

    const ofc = await updateOrphanedFilesConfig(token, clientId, {
      enabled: true,
      scanDirectories: [`${APP_DOWNLOADS}/e2e-abs`],
      orphanedDirectory: `${APP_DOWNLOADS}/complete-orphaned`,
      minFileAgeHours: 0,
    });
    expect(ofc.status).toBe(200);

    const dirName = `claim-abs-${Date.now().toString(36)}`;
    driver.writeArticleFile(dirName, 'data1.bin', 32_768);
    driver.writeArticleFile(dirName, 'data2.bin', 32_768);
    const nzoId = await driver.addWorkingNzbDir(dirName, category);
    await driver.waitForHistoryStatus(nzoId, 'Completed', 60_000);

    // A loose folder with no SABnzbd job behind it, sitting next to the job folder.
    mkdirSync(join(absDir, 'leftover-unrelated'), { recursive: true });

    try {
      const trig = await triggerJob(token, 'DownloadCleaner');
      expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);

      expect(await waitForMove(orphanedDir, 'leftover-unrelated'), 'unrelated folder should have been moved out').toBe(true);
      expect(existsSync(join(absDir, dirName)), 'the completed job folder in the absolute category folder is claimed and must survive').toBe(true);
    } finally {
      await driver.deleteCategory(category);
    }
  });

  test('download_dir and complete_dir survive a scan rooted one level above them, an unrelated sibling moves', async () => {
    test.setTimeout(90_000);
    const completeDir = join(HOST_DOWNLOADS, 'complete');
    const incompleteDir = join(HOST_DOWNLOADS, 'incomplete');
    resetDir(completeDir);
    resetDir(incompleteDir);
    const orphanedDir = join(HOST_DOWNLOADS, 'root-orphaned');
    resetDir(orphanedDir);

    const ofc = await updateOrphanedFilesConfig(token, clientId, {
      enabled: true,
      scanDirectories: [APP_DOWNLOADS],
      orphanedDirectory: `${APP_DOWNLOADS}/root-orphaned`,
      minFileAgeHours: 0,
    });
    expect(ofc.status).toBe(200);

    const dirName = `claim-root-${Date.now().toString(36)}`;
    driver.writeArticleFile(dirName, 'data1.bin', 32_768);
    driver.writeArticleFile(dirName, 'data2.bin', 32_768);
    const nzoId = await driver.addWorkingNzbDir(dirName, 'e2e-ofc');
    await driver.waitForHistoryStatus(nzoId, 'Completed', 60_000);

    // An unrelated top-level entry with no SABnzbd job or config directory behind it.
    mkdirSync(join(HOST_DOWNLOADS, 'leftover-root'), { recursive: true });

    const trig = await triggerJob(token, 'DownloadCleaner');
    expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);

    expect(await waitForMove(orphanedDir, 'leftover-root'), 'unrelated top-level folder should have been moved out').toBe(true);
    expect(existsSync(incompleteDir), 'download_dir itself is claimed and must survive').toBe(true);
    expect(existsSync(completeDir), 'complete_dir itself is claimed and must survive').toBe(true);
    expect(existsSync(join(completeDir, dirName)), 'the completed job folder must survive').toBe(true);
  });

  test('download_dir and complete_dir survive a root-level scan with an idle queue and empty history', async () => {
    test.setTimeout(60_000);
    const completeDir = join(HOST_DOWNLOADS, 'complete');
    const incompleteDir = join(HOST_DOWNLOADS, 'incomplete');
    resetDir(completeDir);
    resetDir(incompleteDir);
    const orphanedDir = join(HOST_DOWNLOADS, 'root-orphaned');
    resetDir(orphanedDir);

    await driver.clearAll();

    const ofc = await updateOrphanedFilesConfig(token, clientId, {
      enabled: true,
      scanDirectories: [APP_DOWNLOADS],
      orphanedDirectory: `${APP_DOWNLOADS}/root-orphaned`,
      minFileAgeHours: 0,
    });
    expect(ofc.status).toBe(200);

    mkdirSync(join(HOST_DOWNLOADS, 'leftover-root'), { recursive: true });

    const trig = await triggerJob(token, 'DownloadCleaner');
    expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);

    expect(await waitForMove(orphanedDir, 'leftover-root'), 'unrelated top-level folder should have been moved out').toBe(true);
    expect(existsSync(incompleteDir), 'download_dir must survive even with an empty history').toBe(true);
    expect(existsSync(completeDir), 'complete_dir must survive even with an empty history').toBe(true);
  });
});

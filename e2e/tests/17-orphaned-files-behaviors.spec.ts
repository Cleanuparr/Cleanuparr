import { test, expect } from '@playwright/test';
import { existsSync, mkdirSync, readdirSync, statSync, utimesSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import {
  loginAndGetToken,
  createDownloadClient,
  listDownloadClients,
  deleteDownloadClient,
  updateDownloadCleanerConfig,
  getDownloadCleanerConfig,
  updateOrphanedFilesConfig,
  getGeneralConfig,
  updateGeneralConfig,
  triggerJob,
  OrphanedFilesConfigRequest,
} from './helpers/app-api';
import { QBittorrentDriver } from './helpers/torrent-clients/qbittorrent';
import { chmodIgnoringEPERM, resetDirectory } from './helpers/torrent-fixtures';

/**
 * Behavior-level coverage for the orphaned files cleaner that isn't
 * client-specific. The per-client integration matrix lives in
 * `16-orphaned-files-cleanup.spec.ts`; this file picks qBittorrent as the
 * single backing client and exercises configuration knobs:
 *
 *   - PurgeAfterHours (deletes aged, leaves recent, null = never purge)
 *   - MinFileAgeHours (skips fresh entries)
 *   - ExcludePatterns
 *   - Per-client config disabled = no-op
 *   - DryRun = read-only
 *
 * "Aged" is simulated by backdating mtime via `utimesSync` after the file
 * exists. This is reliable for the purge path (which only consults
 * `GetLastWriteTimeUtc`) but not for the move path's MinFileAgeHours check,
 * which compares against `MAX(lastWrite, created)` — Linux birthtime
 * cannot be portably backdated. That scenario is covered by unit tests.
 */

const HOST_DOWNLOADS = resolve(__dirname, '..', 'test-data', 'downloads');
const APP_DOWNLOADS = '/e2e-downloads';
const SLUG = 'qbittorrent-behaviors';
const HOST_SCAN_DIR = join(HOST_DOWNLOADS, SLUG);
const HOST_ORPHANED_DIR = join(HOST_DOWNLOADS, SLUG, 'orphaned');
const APP_SCAN_DIR = `${APP_DOWNLOADS}/${SLUG}`;
const APP_ORPHANED_DIR = `${APP_DOWNLOADS}/${SLUG}/orphaned`;

function backdateRecursive(path: string, hoursAgo: number): void {
  const t = (Date.now() - hoursAgo * 3600_000) / 1000;
  const visit = (p: string) => {
    utimesSync(p, t, t);
    if (statSync(p).isDirectory()) {
      for (const e of readdirSync(p)) visit(join(p, e));
    }
  };
  visit(path);
}

function writeOrphanFile(dir: string, name: string, content = 'orphan'): string {
  mkdirSync(dir, { recursive: true });
  chmodIgnoringEPERM(dir, 0o777);
  const path = join(dir, name);
  writeFileSync(path, content);
  return path;
}

async function waitForCondition(
  predicate: () => boolean | Promise<boolean>,
  timeoutMs: number,
  label: string,
): Promise<void> {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (await predicate()) {
      return;
    }
    await new Promise((r) => setTimeout(r, 500));
  }
  throw new Error(`Timed out after ${timeoutMs}ms waiting for: ${label}`);
}

async function triggerAndSettle(token: string): Promise<void> {
  const res = await triggerJob(token, 'DownloadCleaner');
  expect(res.ok, `triggerJob: ${res.status}`).toBe(true);
  // The cleaner is async on a worker thread. Give it time to walk the dirs.
  // No seeding downloads means no 10s arr-settle delay — a couple of seconds
  // is plenty in practice, but we still poll where it matters.
  await new Promise((r) => setTimeout(r, 3000));
}

test.describe.serial('Orphaned files cleanup — behaviors', () => {
  const driver = new QBittorrentDriver();
  let token: string;
  let clientId: string;

  test.beforeAll(async () => {
    token = await loginAndGetToken();

    // Clean slate: remove any leftover clients from other specs.
    const existing = await listDownloadClients(token);
    for (const client of existing) {
      await deleteDownloadClient(token, client.id);
    }

    // Enable the global download cleaner. Schedule is irrelevant — we
    // trigger the job manually.
    const dcCurrent = await (await getDownloadCleanerConfig(token)).json();
    await updateDownloadCleanerConfig(token, {
      enabled: true,
      cronExpression: dcCurrent.cronExpression || '0 0 * * * ?',
      useAdvancedScheduling: dcCurrent.useAdvancedScheduling ?? false,
      ignoredDownloads: [],
    });

    mkdirSync(HOST_DOWNLOADS, { recursive: true });

    // Bring up qBittorrent and register it with Cleanuparr.
    await driver.ready();
    await driver.clearAllTorrents();

    const createRes = await createDownloadClient(token, {
      enabled: true,
      name: 'qBittorrent behaviors',
      typeName: driver.typeName,
      type: 'Torrent',
      host: driver.cleanuparrHost,
      username: driver.username ?? '',
      password: driver.password ?? '',
      downloadDirectorySource: '/downloads',
      downloadDirectoryTarget: APP_SCAN_DIR,
    });
    expect(createRes.ok, `createDownloadClient: ${createRes.status}`).toBe(true);
    const created = await createRes.json();
    clientId = created.id;
  });

  test.beforeEach(async () => {
    // Reset filesystem state before each scenario.
    resetDirectory(HOST_SCAN_DIR);
    mkdirSync(HOST_ORPHANED_DIR, { recursive: true });
    chmodIgnoringEPERM(HOST_ORPHANED_DIR, 0o777);
    // No torrents in the client → claimedPaths is empty → every entry in
    // scan dir is treated as orphan.
    await driver.clearAllTorrents();
  });

  const configureOrphanedFiles = async (
    overrides: Partial<OrphanedFilesConfigRequest> = {},
  ): Promise<void> => {
    const config: OrphanedFilesConfigRequest = {
      enabled: true,
      scanDirectories: [APP_SCAN_DIR],
      orphanedDirectory: APP_ORPHANED_DIR,
      excludePatterns: [],
      minFileAgeHours: 0,
      purgeAfterHours: null,
      ...overrides,
    };
    const res = await updateOrphanedFilesConfig(token, clientId, config);
    expect(res.ok, `updateOrphanedFilesConfig: ${res.status}`).toBe(true);
  };

  test('PurgeAfterHours deletes aged entries from the orphaned directory', async () => {
    test.setTimeout(60_000);

    const aged = writeOrphanFile(HOST_ORPHANED_DIR, 'aged.bin');
    backdateRecursive(aged, 25);
    await configureOrphanedFiles({ purgeAfterHours: 24 });

    await triggerAndSettle(token);
    await waitForCondition(() => !existsSync(aged), 10_000, `purge of ${aged}`);
  });

  test('PurgeAfterHours leaves entries newer than the cutoff', async () => {
    test.setTimeout(60_000);

    const fresh = writeOrphanFile(HOST_ORPHANED_DIR, 'fresh.bin');
    await configureOrphanedFiles({ purgeAfterHours: 24 });

    await triggerAndSettle(token);
    expect(existsSync(fresh)).toBe(true);
  });

  test('PurgeAfterHours null never purges, even very old entries', async () => {
    test.setTimeout(60_000);

    const ancient = writeOrphanFile(HOST_ORPHANED_DIR, 'ancient.bin');
    backdateRecursive(ancient, 24 * 365);
    await configureOrphanedFiles({ purgeAfterHours: null });

    await triggerAndSettle(token);
    expect(existsSync(ancient)).toBe(true);
  });

  test('MinFileAgeHours skips fresh entries in the scan directory', async () => {
    test.setTimeout(60_000);

    const fresh = writeOrphanFile(HOST_SCAN_DIR, 'too-fresh.bin');
    await configureOrphanedFiles({ minFileAgeHours: 1 });

    await triggerAndSettle(token);
    // Still in the scan dir, not moved to orphaned dir.
    expect(existsSync(fresh)).toBe(true);
    expect(existsSync(join(HOST_ORPHANED_DIR, 'too-fresh.bin'))).toBe(false);
  });

  test('ExcludePatterns prevents matching entries from being moved', async () => {
    test.setTimeout(60_000);

    const excluded = writeOrphanFile(HOST_SCAN_DIR, 'metadata.nfo');
    const matched = writeOrphanFile(HOST_SCAN_DIR, 'real-orphan.bin');
    await configureOrphanedFiles({ excludePatterns: ['*.nfo'] });

    await triggerAndSettle(token);
    await waitForCondition(
      () => !existsSync(matched),
      10_000,
      'real-orphan.bin to be moved',
    );
    // .nfo file untouched.
    expect(existsSync(excluded)).toBe(true);
  });

  test('Disabled per-client config is a no-op', async () => {
    test.setTimeout(60_000);

    const orphan = writeOrphanFile(HOST_SCAN_DIR, 'leave-me.bin');
    await configureOrphanedFiles({ enabled: false });

    await triggerAndSettle(token);
    expect(existsSync(orphan)).toBe(true);
    expect(existsSync(join(HOST_ORPHANED_DIR, 'leave-me.bin'))).toBe(false);
  });

  test.describe('DryRun', () => {
    test.afterEach(async () => {
      // Always clear dry-run so it doesn't leak into subsequent specs.
      const current = await getGeneralConfig(token);
      await updateGeneralConfig(token, { ...current, dryRun: false });
    });

    test('DryRun skips filesystem mutations', async () => {
      test.setTimeout(60_000);

      const orphan = writeOrphanFile(HOST_SCAN_DIR, 'pretend-only.bin');

      const current = await getGeneralConfig(token);
      await updateGeneralConfig(token, { ...current, dryRun: true });

      await configureOrphanedFiles();

      await triggerAndSettle(token);
      expect(existsSync(orphan)).toBe(true);
      expect(existsSync(join(HOST_ORPHANED_DIR, 'pretend-only.bin'))).toBe(false);
    });
  });
});

import { resolve } from 'node:path';
import { test, expect } from '../fixtures/base';
import { indexerMock } from '../helpers/live-arr';
import { RADARR, RUN_TAG, SONARR, resetLiveArrState, teardownInstances } from '../helpers/seeker-live';
import type { SeededArr } from '../helpers/seeker-live';
import { buildSingleFileTorrent, buildSparseSingleFileTorrent } from '../helpers/torrent-fixtures';
import { torznabSearchStub, torznabTorrentStub } from '../helpers/mocks/torznab-stubs';
import type { CleanuparrApi } from '../helpers/api';

/**
 * Force import against real Sonarr and Radarr, from the block to the imported file.
 *
 * Both arrs park a download they cannot read a runtime from in importPending,
 * with "Unable to determine if file is a sample". The file itself is fine.
 */

/** qBittorrent's save path, mounted into both arrs at the same path so the import resolves. */
const QBIT_DOWNLOADS = resolve(__dirname, '..', '..', 'test-data', 'downloads', 'qbittorrent');

/** One of the reasons Cleanuparr treats as safe to force past. */
const SAMPLE_PATTERN = 'Unable to determine if file is a sample';

const ANNOUNCE = 'http://127.0.0.1:6969/announce';

interface ForceImportTarget {
  arr: SeededArr;
  /** Search command that makes the arr grab the release the indexer offers. */
  searchCommand: Record<string, unknown>;
  /** Endpoint listing the files the arr imported for the seeded item. */
  filesPath: string;
  /** Endpoint that deletes one of those files. */
  filePath: (id: number) => string;
  /** An inner file name the arr blocks for a reason Cleanuparr does not recognise. */
  unconfiguredReason: { innerName: string; message: string };
  /** A release whose title does not parse to the seeded item, but whose id attribute does. */
  byId: { releaseTitle: string; attr: string; idField: string; message: string };
}

const TARGETS: ForceImportTarget[] = [
  {
    arr: SONARR,
    searchCommand: { name: 'EpisodeSearch', episodeIds: [1] },
    filesPath: '/api/v3/episodefile?seriesId=1',
    filePath: (id) => `/api/v3/episodefile/${id}`,
    // Sonarr parses this as another episode of the grabbed release.
    unconfiguredReason: { innerName: 'E2E.Mismatch', message: 'was not found in the grabbed release' },
    byId: {
      releaseTitle: `Unknown.Show.Name.S01E01.1080p.WEB-DL-E2E${RUN_TAG}`,
      attr: 'tvdbid',
      idField: 'tvdbId',
      message: 'matched to series by ID',
    },
  },
  {
    arr: RADARR,
    searchCommand: { name: 'MoviesSearch', movieIds: [1] },
    filesPath: '/api/v3/moviefile?movieId=1',
    filePath: (id) => `/api/v3/moviefile/${id}`,
    // Radarr cannot parse a movie out of this at all.
    unconfiguredReason: { innerName: 'E2E.Unparsable', message: 'Unable to parse file' },
    byId: {
      releaseTitle: `Unknown.Movie.Name.2025.1080p.WEB-DL-E2E${RUN_TAG}`,
      attr: 'tmdbid',
      idField: 'tmdbId',
      message: 'matched to movie by ID',
    },
  },
];

/**
 * Grabs a release whose payload already sits in the client's save path.
 *
 * qBittorrent completes it on its hash check, so the arr reaches its import.
 */
async function grabCompletedRelease(
  target: ForceImportTarget,
  releaseTitle: string,
  innerFileName: string,
  attrs?: Record<string, string | number>,
  sparseBytes?: number,
): Promise<string> {
  const torrent = sparseBytes
    ? buildSparseSingleFileTorrent(QBIT_DOWNLOADS, innerFileName, sparseBytes, ANNOUNCE)
    : buildSingleFileTorrent(QBIT_DOWNLOADS, innerFileName, 32_768, ANNOUNCE);
  const file = `${releaseTitle}.torrent`;

  await indexerMock.stubMany([
    torznabSearchStub(target.arr.searchMode, [{ title: releaseTitle, category: target.arr.category, file, attrs }]),
    torznabTorrentStub(file, torrent.metainfo),
  ]);

  await target.arr.arr.post('/api/v3/command', target.searchCommand);

  return torrent.infoHash.toUpperCase();
}

/** Waits for the arr to park the grab on its import, and returns the reasons it gave. */
async function waitForImportBlock(
  target: ForceImportTarget,
  downloadId: string,
  timeoutMs = 180_000,
): Promise<string[]> {
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    await target.arr.arr.refreshMonitoredDownloads();
    await new Promise((r) => setTimeout(r, 2_000));

    const records = await target.arr.arr.queue();
    const record = records.find((r) => r.downloadId?.toUpperCase() === downloadId) as
      | (Record<string, unknown> & { statusMessages?: Array<{ messages?: string[] }> })
      | undefined;

    if (!record || record.trackedDownloadState === 'downloading') {
      continue;
    }

    const messages = (record.statusMessages ?? []).flatMap((m) => m.messages ?? []);

    if (messages.length > 0) {
      return messages;
    }
  }

  throw new Error(`${target.arr.type} never blocked the import of ${downloadId}`);
}

async function importedFiles(target: ForceImportTarget): Promise<Array<{ id: number; relativePath: string }>> {
  return target.arr.arr.get(target.filesPath);
}

/** Force import writes into the library, which the next run must not inherit. */
async function clearImportedFiles(): Promise<void> {
  for (const target of TARGETS) {
    for (const file of await importedFiles(target)) {
      await target.arr.arr.delete(target.filePath(file.id));
    }
  }
}

async function arrangeForceImport(api: CleanuparrApi, target: ForceImportTarget): Promise<void> {
  const current = await (await api.queueCleaner.getConfig()).json();
  const updated = await api.queueCleaner.updateConfig({
    ...current,
    failedImport: {
      ...current.failedImport,
      // Striking stays off, so only force import can clear the queue.
      maxStrikes: 0,
      forceImport: true,
    },
  });

  expect(updated.ok, `queue cleaner updateConfig: ${updated.status} ${await updated.text()}`).toBe(true);

  const created = await api.arr.createInstance(target.arr.type, {
    name: `E2E force import ${target.arr.type} ${RUN_TAG}`,
    url: target.arr.url,
    apiKey: target.arr.apiKey,
    version: target.arr.version,
    enabled: true,
  });

  expect(created.ok, `createInstance: ${created.status}`).toBe(true);
}

for (const target of TARGETS) {
  test.describe.serial(`Force import against a live ${target.arr.type}`, () => {
    test.beforeEach(async () => {
      await resetLiveArrState();
      await clearImportedFiles();
    });

    test.afterEach(async ({ api }) => {
      await teardownInstances(api);
      await resetLiveArrState();
      await clearImportedFiles();
    });

    test('imports a download the arr blocked on sample detection', async ({ api }) => {
      test.setTimeout(600_000);

      const release = `${target.arr.release}-FI`;
      const downloadId = await grabCompletedRelease(target, release, `${release}.mkv`);

      // The block Cleanuparr is meant to rescue, straight from the arr.
      // A 32 KB payload reads as a definite sample now and then, which blocks it for a
      // reason force import does not recognise. Playwright's retry covers that.
      const messages = await waitForImportBlock(target, downloadId);
      expect(messages).toContain(SAMPLE_PATTERN);

      await arrangeForceImport(api, target);

      // Two runs: importPending is transitional, so the first only records the sighting.
      await expect
        .poll(
          async () => {
            await api.jobs.trigger('QueueCleaner');
            return (await importedFiles(target)).length;
          },
          { timeout: 240_000, intervals: [5_000] },
        )
        .toBe(1);

      expect((await importedFiles(target))[0].relativePath).toContain(release);
      await expect.poll(async () => (await target.arr.arr.queue()).length, { timeout: 60_000 }).toBe(0);
    });

    test('imports a download the arr blocked on matching the item by ID', async ({ api }) => {
      test.setTimeout(600_000);

      // Read the seeded item's id at run time, so a reseed cannot silently break this.
      const item = await target.arr.arr.get<Record<string, unknown>>(
        `/api/v3/${target.arr.itemPath}/${target.arr.itemId}`,
      );
      const id = item[target.byId.idField];

      const release = target.byId.releaseTitle;
      const downloadId = await grabCompletedRelease(target, release, `${release}.mkv`, {
        [target.byId.attr]: id as number,
      });

      const messages = await waitForImportBlock(target, downloadId);
      expect(messages.some((m) => m.includes(target.byId.message))).toBe(true);

      await arrangeForceImport(api, target);

      // importBlocked is already settled, so one run is enough to force the import.
      await api.jobs.trigger('QueueCleaner');
      await expect
        .poll(async () => (await importedFiles(target)).length, { timeout: 120_000, intervals: [5_000] })
        .toBe(1);

      expect((await importedFiles(target))[0].relativePath).toContain(release);
      await expect.poll(async () => (await target.arr.arr.queue()).length, { timeout: 60_000 }).toBe(0);
    });

    // A live arr repeats the same reasons on the queue item.
    // Only the mock spec can reach the candidate-level triage.
    test('leaves a download blocked for an unrecognised reason alone', async ({ api }) => {
      test.setTimeout(600_000);

      const release = `${target.arr.release}-FX`;
      const downloadId = await grabCompletedRelease(
        target,
        release,
        `${target.unconfiguredReason.innerName}.${RUN_TAG}.mkv`,
      );

      const messages = await waitForImportBlock(target, downloadId);
      expect(messages.some((m) => m.includes(target.unconfiguredReason.message))).toBe(true);

      await arrangeForceImport(api, target);

      for (let run = 0; run < 3; run++) {
        await api.jobs.trigger('QueueCleaner');
        await new Promise((r) => setTimeout(r, 5_000));
      }

      expect(await importedFiles(target)).toHaveLength(0);
      expect(
        (await target.arr.arr.queue()).some((r) => r.downloadId?.toUpperCase() === downloadId),
      ).toBe(true);
    });
  });
}

/**
 * Big enough that the arr spends seconds copying it from /downloads to /tv.
 *
 * The two paths are separate bind mounts, so the arr cannot rename across them.
 * The payload is sparse, so the source costs no disk.
 */
const SLOW_IMPORT_BYTES = 512 * 1024 * 1024;

const SONARR_TARGET = TARGETS.find((t) => t.arr.type === 'sonarr')!;

/**
 * Force import counts a try per import it asks for, and leans on the arr's own
 * command list to know when to keep quiet.
 *
 * That only holds if a ManualImport command stays open for as long as the arr
 * spends importing. If the arr closed the command and imported afterwards,
 * every slow import would burn tries while the arr was still working.
 */
test.describe.serial('A ManualImport command covers the arr\'s whole import', () => {
  test.beforeEach(async () => {
    await resetLiveArrState();
    await clearImportedFiles();
  });

  test.afterEach(async ({ api }) => {
    await teardownInstances(api);
    await resetLiveArrState();
    await clearImportedFiles();
  });

  test('reports the command as running until the imported file exists', async ({ api }) => {
    test.setTimeout(900_000);

    const series = await SONARR_TARGET.arr.arr.get<Record<string, unknown>>(
      `/api/v3/${SONARR_TARGET.arr.itemPath}/${SONARR_TARGET.arr.itemId}`,
    );

    const release = `${SONARR_TARGET.byId.releaseTitle}-SLOW`;
    const downloadId = await grabCompletedRelease(
      SONARR_TARGET,
      release,
      `${release}.mkv`,
      { [SONARR_TARGET.byId.attr]: series[SONARR_TARGET.byId.idField] as number },
      SLOW_IMPORT_BYTES,
    );

    const messages = await waitForImportBlock(SONARR_TARGET, downloadId);
    expect(messages.some((m) => m.includes(SONARR_TARGET.byId.message))).toBe(true);

    // /downloads and /tv sit on one filesystem, so the arr would hardlink and
    // the import would cost no time at all.
    const mediaManagement = await SONARR_TARGET.arr.arr.get<Record<string, unknown>>(
      '/api/v3/config/mediamanagement',
    );
    await SONARR_TARGET.arr.arr.put('/api/v3/config/mediamanagement', {
      ...mediaManagement,
      copyUsingHardlinks: false,
    });

    await arrangeForceImport(api, SONARR_TARGET);
    await api.jobs.trigger('QueueCleaner');

    // Sampled while the arr works, so a closed-early command shows up as a
    // sample that reports no running import while no file has landed either.
    const samples: Array<{ running: boolean; files: number }> = [];
    const deadline = Date.now() + 300_000;

    while (Date.now() < deadline) {
      const [commands, files] = await Promise.all([
        SONARR_TARGET.arr.arr.commands(),
        importedFiles(SONARR_TARGET),
      ]);

      const running = commands.some(
        (c) => c.name === 'ManualImport' && (c.status === 'queued' || c.status === 'started'),
      );

      samples.push({ running, files: files.length });

      if (files.length > 0) {
        break;
      }

      await new Promise((r) => setTimeout(r, 100));
    }

    expect(samples.at(-1)?.files, 'the arr never imported the file').toBe(1);

    const firstRunning = samples.findIndex((s) => s.running);
    expect(firstRunning, 'the arr never reported the ManualImport as running').toBeGreaterThanOrEqual(0);

    // A command that closed early leaves a gap: nothing running, no file yet.
    // Two samples wide, so one unlucky read between the two calls does not count.
    const tail = samples.slice(firstRunning);
    const gap = tail.findIndex(
      (s, i) => !s.running && s.files === 0 && tail[i + 1] && !tail[i + 1].running && tail[i + 1].files === 0,
    );

    expect(gap, 'the arr closed the ManualImport before the import landed').toBe(-1);
  });
});

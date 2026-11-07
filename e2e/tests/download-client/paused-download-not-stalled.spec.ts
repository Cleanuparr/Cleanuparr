import { join, resolve } from 'node:path';
import { rmSync } from 'node:fs';
import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { CleanuparrApi } from '../helpers/api';
import { buildDownloadClientPayload } from '../helpers/api/download-client';
import { ArrStubs } from '../helpers/mocks';
import { MockServers } from '../helpers/mocks/wiremock-client';
import { RTorrentDriver } from '../helpers/torrent-clients/rtorrent';
import { UTorrentDriver } from '../helpers/torrent-clients/utorrent';
import type { TorrentClientDriver } from '../helpers/torrent-clients/types';
import { buildFolderTorrent } from '../helpers/torrent-fixtures';

/**
 * A paused download must not collect stall strikes (#880).
 * rTorrent: d.pause leaves d.state at 1; only d.is_active marks it paused.
 * uTorrent: pause sets the Paused bit and keeps Started; a seeding torrent also reports no speed or ETA.
 */

const HOST_DOWNLOADS = resolve(__dirname, '..', '..', 'test-data', 'downloads');
const CLIENT_DOWNLOADS = '/downloads';
const SERIES_ID = 1;
const EPISODE_ID = 2;
const QUEUE_ITEM_ID = 7101;

const rtorrent = new RTorrentDriver();
const utorrent = new UTorrentDriver();

interface ClientUnderTest {
  driver: TorrentClientDriver;
  type: 'rtorrent' | 'utorrent';
  downloadsDir: string;
}

const RTORRENT: ClientUnderTest = { driver: rtorrent, type: 'rtorrent', downloadsDir: 'rtorrent' };
const UTORRENT: ClientUnderTest = { driver: utorrent, type: 'utorrent', downloadsDir: 'utorrent' };

interface ArrangeOptions {
  keepData?: boolean;
  trackedDownloadState?: string;
  sizeleft?: number;
}

async function stubHealthyLookingQueue(
  mocks: MockServers,
  downloadId: string,
  trackedDownloadState: string,
  sizeleft: number,
): Promise<void> {
  await ArrStubs.applyArrDefaults(mocks.arr);
  await mocks.arr.stub(
    ArrStubs.arrRawQueueStub(
      JSON.stringify({
        page: 1,
        pageSize: 50,
        totalRecords: 1,
        records: [
          {
            id: QUEUE_ITEM_ID,
            seriesId: SERIES_ID,
            episodeId: EPISODE_ID,
            title: 'paused.test.s01e02',
            status: 'ok',
            trackedDownloadStatus: 'ok',
            trackedDownloadState,
            downloadId,
            protocol: 'torrent',
            size: 32768,
            sizeleft,
          },
        ],
      }),
    ),
  );
}

async function countEvents(api: CleanuparrApi, eventType: string, downloadId: string): Promise<number> {
  const res = await api.events.list({ eventType, page: 1, pageSize: 500 });
  expect(res.status, 'events query failed').toBe(200);

  const body: { items?: Array<{ itemHash?: string }> } = await res.json();

  return (body.items ?? []).filter((e) => (e.itemHash ?? '').toLowerCase() === downloadId.toLowerCase()).length;
}

async function queueGetCount(mocks: MockServers): Promise<number> {
  return (await mocks.arr.findRequests({ method: 'GET', urlPath: '/api/v3/queue' })).length;
}

async function runQueueCleaner(api: CleanuparrApi, mocks: MockServers): Promise<void> {
  const before = await queueGetCount(mocks);
  expect((await api.jobs.trigger('QueueCleaner')).status).toBeLessThan(300);

  await expect
    .poll(() => queueGetCount(mocks), {
      message: 'QueueCleaner run never queried the *arr queue',
      timeout: 60_000,
    })
    .toBeGreaterThan(before);
}

interface Arrangement {
  hash: string;
  clientId: string;
  arrInstanceId: string;
  ruleId: string;
}

async function waitForRTorrentDownloading(hash: string): Promise<void> {
  await expect
    .poll(
      async () => [await rtorrent.getComplete(hash), await rtorrent.getState(hash), await rtorrent.getIsActive(hash)],
      { message: 'rTorrent never reported an active, incomplete download', timeout: 60_000 },
    )
    .toEqual([0, 1, 1]);
}

const UTORRENT_STARTED = 1;
const UTORRENT_CHECKED = 8;
const UTORRENT_PAUSED = 32;

function waitForUTorrent(completed: boolean): (hash: string) => Promise<void> {
  return async (hash) => {
    await expect
      .poll(
        async () => {
          const row = await utorrent.getStatusAndCompleted(hash);
          return (
            row !== undefined &&
            (row.status & UTORRENT_STARTED) !== 0 &&
            (row.status & UTORRENT_CHECKED) !== 0 &&
            (row.status & UTORRENT_PAUSED) === 0 &&
            row.dateCompleted > 0 === completed
          );
        },
        { message: `uTorrent never reported a started torrent with completed=${completed}`, timeout: 60_000 },
      )
      .toBe(true);
  };
}

/**
 * Adds a started torrent and points Cleanuparr at it with a stall rule.
 * Without keepData the torrent stays incomplete.
 */
async function arrange(
  api: CleanuparrApi,
  mocks: MockServers,
  label: string,
  client: ClientUnderTest,
  waitUntilAdded: (hash: string) => Promise<void>,
  options: ArrangeOptions = {},
): Promise<Arrangement> {
  await client.driver.ready();
  await client.driver.clearAllTorrents();

  const torrent = buildFolderTorrent(join(HOST_DOWNLOADS, client.downloadsDir), `${label}-${Date.now()}`);
  if (!options.keepData) {
    // With no data on disk the start-time hash check leaves the torrent incomplete.
    rmSync(torrent.contentPath, { recursive: true, force: true });
  }
  await client.driver.addTorrent({
    metainfo: torrent.metainfo,
    savePath: CLIENT_DOWNLOADS,
    name: torrent.name,
    infoHash: torrent.infoHash,
  });

  await waitUntilAdded(torrent.infoHash);

  await stubHealthyLookingQueue(
    mocks,
    torrent.infoHash,
    options.trackedDownloadState ?? 'downloading',
    options.sizeleft ?? 32768,
  );

  const created = await api.downloadClient.create(
    buildDownloadClientPayload(client.type, {
      name: `${client.type}-paused-${Date.now()}`,
      host: client.driver.cleanuparrHost,
      username: client.driver.username,
      password: client.driver.password,
    }),
  );
  const createdBody = await created.text();
  expect(created.status, createdBody).toBeLessThan(300);

  const arr = await api.arr.createInstance('sonarr', {
    name: `sonarr-paused-${Date.now()}`,
    url: TEST_CONFIG.mocks.arrUrl,
    apiKey: 'k',
    version: 3,
    enabled: true,
  });
  const arrBody = await arr.text();
  expect(arr.status, arrBody).toBeLessThan(300);

  // Every fixture torrent is private.
  const rule = await api.queueCleaner.createRule('stall', {
    name: `paused-stall-${Date.now()}`,
    enabled: true,
    maxStrikes: 3,
    privacyType: 'Both',
    minCompletionPercentage: 0,
    maxCompletionPercentage: 100,
    deletePrivateTorrentsFromClient: false,
    changeCategory: false,
    resetStrikesOnProgress: false,
    minimumProgress: null,
  });
  const ruleBody = await rule.text();
  expect(rule.status, ruleBody).toBeLessThan(300);

  return {
    hash: torrent.infoHash,
    clientId: JSON.parse(createdBody).id as string,
    arrInstanceId: JSON.parse(arrBody).id as string,
    ruleId: JSON.parse(ruleBody).id as string,
  };
}

async function cleanup(api: CleanuparrApi, arranged: Arrangement | undefined, client: ClientUnderTest): Promise<void> {
  if (arranged) {
    await api.queueCleaner.deleteRule('stall', arranged.ruleId);
    await api.arr.deleteInstance('sonarr', arranged.arrInstanceId);
    await api.downloadClient.delete(arranged.clientId);
  }
  await client.driver.clearAllTorrents();
}

test.describe('Queue Cleaner stall check on rTorrent', () => {
  test('an active incomplete download gets a stall strike', async ({ api, mocks }) => {
    test.setTimeout(180_000);

    let arranged: Arrangement | undefined;

    try {
      arranged = await arrange(api, mocks, 'stall-control', RTORRENT, waitForRTorrentDownloading);
      await runQueueCleaner(api, mocks);

      await expect
        .poll(() => countEvents(api, 'StalledStrike', arranged!.hash), {
          message: 'the stall strike was never recorded',
          timeout: 60_000,
        })
        .toBe(1);
    } finally {
      await cleanup(api, arranged, RTORRENT);
    }
  });

  test('a paused download gets no stall strike (#880)', async ({ api, mocks }) => {
    test.setTimeout(180_000);

    let arranged: Arrangement | undefined;

    try {
      arranged = await arrange(api, mocks, 'stall-paused', RTORRENT, waitForRTorrentDownloading);

      await rtorrent.pause(arranged.hash);
      expect(await rtorrent.getState(arranged.hash), 'd.pause leaves d.state at 1').toBe(1);
      expect(await rtorrent.getIsActive(arranged.hash), 'd.pause clears d.is_active').toBe(0);

      await runQueueCleaner(api, mocks);
      // The second run starts only after the first finishes.
      await runQueueCleaner(api, mocks);

      expect(await countEvents(api, 'StalledStrike', arranged.hash)).toBe(0);
      expect(await mocks.arr.findRequests({ method: 'DELETE', urlPattern: '/api/v3/queue/.*' })).toHaveLength(0);
    } finally {
      await cleanup(api, arranged, RTORRENT);
    }
  });
});

test.describe('Queue Cleaner stall check on uTorrent', () => {
  test('an incomplete download with no speed or ETA gets a stall strike', async ({ api, mocks }) => {
    test.setTimeout(180_000);

    let arranged: Arrangement | undefined;

    try {
      arranged = await arrange(api, mocks, 'stall-control', UTORRENT, waitForUTorrent(false));
      await runQueueCleaner(api, mocks);

      await expect
        .poll(() => countEvents(api, 'StalledStrike', arranged!.hash), {
          message: 'the stall strike was never recorded',
          timeout: 60_000,
        })
        .toBe(1);
    } finally {
      await cleanup(api, arranged, UTORRENT);
    }
  });

  test('a paused download gets no stall strike', async ({ api, mocks }) => {
    test.setTimeout(180_000);

    let arranged: Arrangement | undefined;

    try {
      arranged = await arrange(api, mocks, 'stall-paused', UTORRENT, waitForUTorrent(false));

      await utorrent.pause(arranged.hash);
      await expect
        .poll(async () => ((await utorrent.getStatus(arranged!.hash)) ?? 0) & UTORRENT_PAUSED, {
          message: 'uTorrent never set the Paused bit',
          timeout: 30_000,
        })
        .toBe(UTORRENT_PAUSED);
      expect((await utorrent.getStatus(arranged.hash))! & UTORRENT_STARTED, 'pause keeps the Started bit').toBe(UTORRENT_STARTED);

      await runQueueCleaner(api, mocks);
      // The second run starts only after the first finishes.
      await runQueueCleaner(api, mocks);

      expect(await countEvents(api, 'StalledStrike', arranged.hash)).toBe(0);
      expect(await mocks.arr.findRequests({ method: 'DELETE', urlPattern: '/api/v3/queue/.*' })).toHaveLength(0);
    } finally {
      await cleanup(api, arranged, UTORRENT);
    }
  });

  test('a seeding download gets no stall strike', async ({ api, mocks }) => {
    test.setTimeout(180_000);

    let arranged: Arrangement | undefined;

    try {
      arranged = await arrange(api, mocks, 'stall-seeding', UTORRENT, waitForUTorrent(true), {
        keepData: true,
        trackedDownloadState: 'importPending',
        sizeleft: 0,
      });
      await runQueueCleaner(api, mocks);
      // The second run starts only after the first finishes.
      await runQueueCleaner(api, mocks);

      expect(await countEvents(api, 'StalledStrike', arranged.hash)).toBe(0);
      expect(await mocks.arr.findRequests({ method: 'DELETE', urlPattern: '/api/v3/queue/.*' })).toHaveLength(0);
    } finally {
      await cleanup(api, arranged, UTORRENT);
    }
  });
});

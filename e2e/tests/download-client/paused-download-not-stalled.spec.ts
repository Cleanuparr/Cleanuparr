import { join, resolve } from 'node:path';
import { rmSync } from 'node:fs';
import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { CleanuparrApi } from '../helpers/api';
import { buildDownloadClientPayload } from '../helpers/api/download-client';
import { ArrStubs } from '../helpers/mocks';
import { MockServers } from '../helpers/mocks/wiremock-client';
import { RTorrentDriver } from '../helpers/torrent-clients/rtorrent';
import { buildFolderTorrent } from '../helpers/torrent-fixtures';

/**
 * A paused rTorrent download must not collect stall strikes (#880).
 * d.pause leaves d.state at 1; only d.is_active marks it paused.
 */

const HOST_DOWNLOADS = resolve(__dirname, '..', '..', 'test-data', 'downloads');
const CLIENT_DOWNLOADS = '/downloads';
const SERIES_ID = 1;
const EPISODE_ID = 2;
const QUEUE_ITEM_ID = 7101;

const rtorrent = new RTorrentDriver();

async function stubHealthyLookingQueue(mocks: MockServers, downloadId: string): Promise<void> {
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
            trackedDownloadState: 'downloading',
            downloadId,
            protocol: 'torrent',
            size: 32768,
            sizeleft: 32768,
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

/** Adds an incomplete, started rTorrent download and points Cleanuparr at it with a stall rule. */
async function arrange(api: CleanuparrApi, mocks: MockServers, label: string): Promise<Arrangement> {
  await rtorrent.ready();
  await rtorrent.clearAllTorrents();

  const torrent = buildFolderTorrent(join(HOST_DOWNLOADS, 'rtorrent'), `${label}-${Date.now()}`);
  // With no data on disk the start-time hash check leaves the torrent incomplete.
  rmSync(torrent.contentPath, { recursive: true, force: true });
  await rtorrent.addTorrent({
    metainfo: torrent.metainfo,
    savePath: CLIENT_DOWNLOADS,
    name: torrent.name,
    infoHash: torrent.infoHash,
  });

  await expect
    .poll(
      async () => [
        await rtorrent.getComplete(torrent.infoHash),
        await rtorrent.getState(torrent.infoHash),
        await rtorrent.getIsActive(torrent.infoHash),
      ],
      { message: 'rTorrent never reported an active, incomplete download', timeout: 60_000 },
    )
    .toEqual([0, 1, 1]);

  await stubHealthyLookingQueue(mocks, torrent.infoHash);

  const created = await api.downloadClient.create(
    buildDownloadClientPayload('rtorrent', {
      name: `rtorrent-paused-${Date.now()}`,
      host: rtorrent.cleanuparrHost,
      username: rtorrent.username,
      password: rtorrent.password,
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

async function cleanup(api: CleanuparrApi, arranged: Arrangement | undefined): Promise<void> {
  if (arranged) {
    await api.queueCleaner.deleteRule('stall', arranged.ruleId);
    await api.arr.deleteInstance('sonarr', arranged.arrInstanceId);
    await api.downloadClient.delete(arranged.clientId);
  }
  await rtorrent.clearAllTorrents();
}

test.describe('Queue Cleaner stall check on rTorrent', () => {
  test('an active incomplete download gets a stall strike', async ({ api, mocks }) => {
    test.setTimeout(180_000);

    let arranged: Arrangement | undefined;

    try {
      arranged = await arrange(api, mocks, 'stall-control');
      await runQueueCleaner(api, mocks);

      await expect
        .poll(() => countEvents(api, 'StalledStrike', arranged!.hash), {
          message: 'the stall strike was never recorded',
          timeout: 60_000,
        })
        .toBe(1);
    } finally {
      await cleanup(api, arranged);
    }
  });

  test('a paused download gets no stall strike (#880)', async ({ api, mocks }) => {
    test.setTimeout(180_000);

    let arranged: Arrangement | undefined;

    try {
      arranged = await arrange(api, mocks, 'stall-paused');

      await rtorrent.pause(arranged.hash);
      expect(await rtorrent.getState(arranged.hash), 'd.pause leaves d.state at 1').toBe(1);
      expect(await rtorrent.getIsActive(arranged.hash), 'd.pause clears d.is_active').toBe(0);

      await runQueueCleaner(api, mocks);
      // The second run starts only after the first finishes.
      await runQueueCleaner(api, mocks);

      expect(await countEvents(api, 'StalledStrike', arranged.hash)).toBe(0);
      expect(await mocks.arr.findRequests({ method: 'DELETE', urlPattern: '/api/v3/queue/.*' })).toHaveLength(0);
    } finally {
      await cleanup(api, arranged);
    }
  });
});

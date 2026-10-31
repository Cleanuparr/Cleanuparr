import { test, expect } from '../fixtures/base';
import type { CleanuparrApi } from '../helpers/api';
import type { ArrType } from '../helpers/api/arr';
import { indexerMock } from '../helpers/live-arr';
import type { LiveArr } from '../helpers/live-arr';
import {
  RADARR,
  RUN_TAG,
  SONARR,
  arrangeQueueCleaner,
  resetLiveArrState,
  teardownQueueCleaner,
  waitForArrQueue,
} from '../helpers/seeker-live';
import { grabbableReleases } from '../helpers/mocks/torznab-stubs';

/**
 * #381: stall rules scoped to new downloads vs upgrades.
 *
 * Grabs two stalled downloads against the real Sonarr and Radarr containers.
 * One targets an item with a file (upgrade), the other an item without one (new).
 * Checks that a New-scoped rule and an Upgrade-scoped rule strike each independently.
 *
 * The grab bypasses the Seeker: both default quality profiles have upgradeAllowed off.
 * The arrs never offer a cutoff-unmet candidate on their own.
 * A manual release search and grab, like the arr's "interactive search" button, returns the release with downloadAllowed=true.
 * That puts it in the queue.
 */

const STRIKE_TIMEOUT = 60_000;
const NEW_MAX_STRIKES = 3;
// Each run strikes both downloads together.
// A smaller gap races the delay between a trigger and its strike showing up.
const UPGRADE_MAX_STRIKES = 7;

interface ContentTypeCase {
  label: string;
  arrType: ArrType;
  arr: LiveArr;
  url: string;
  apiKey: string;
  version: number;
  searchMode: 'tvsearch' | 'movie';
  category: number;
  queryParam: 'episodeId' | 'movieId';
  /** Seeded id with no file. */
  newItemId: number;
  /** Id the seed gave a file. */
  upgradeItemId: number;
  newTitle: string;
  upgradeTitle: string;
}

const SONARR_CASE: ContentTypeCase = {
  label: 'sonarr',
  arrType: 'sonarr',
  arr: SONARR.arr,
  url: SONARR.url,
  apiKey: SONARR.apiKey,
  version: SONARR.version,
  searchMode: SONARR.searchMode,
  category: SONARR.category,
  queryParam: 'episodeId',
  newItemId: 4,
  upgradeItemId: 5,
  newTitle: `Agatha.All.Along.S01E04.1080p.WEB-DL.DDP5.1.H.264-E2ECT${RUN_TAG}`,
  upgradeTitle: `Agatha.All.Along.S01E05.1080p.WEB-DL.DDP5.1.H.264-E2ECT${RUN_TAG}`,
};

const RADARR_CASE: ContentTypeCase = {
  label: 'radarr',
  arrType: 'radarr',
  arr: RADARR.arr,
  url: RADARR.url,
  apiKey: RADARR.apiKey,
  version: RADARR.version,
  searchMode: RADARR.searchMode,
  category: RADARR.category,
  queryParam: 'movieId',
  newItemId: 1,
  upgradeItemId: 3,
  newTitle: `F1.2025.1080p.WEB-DL.DDP5.1.H.264-E2ECT${RUN_TAG}`,
  upgradeTitle: `Weapons.2025.1080p.WEB-DL.DDP5.1.H.264-E2ECT${RUN_TAG}`,
};

interface StrikeGroup {
  downloadId: string;
  totalStrikes: number;
}

async function strikeGroup(api: CleanuparrApi, downloadId: string): Promise<StrikeGroup | undefined> {
  const body = await (await api.strikes.list({ pageSize: 200 })).json();
  return (body.items ?? []).find(
    (item: StrikeGroup) => item.downloadId.toLowerCase() === downloadId.toLowerCase(),
  );
}

async function totalStrikes(api: CleanuparrApi, downloadId: string): Promise<number> {
  return (await strikeGroup(api, downloadId))?.totalStrikes ?? 0;
}

async function isQueued(arr: LiveArr, downloadId: string): Promise<boolean> {
  await arr.refreshMonitoredDownloads();
  return (await arr.queue()).some((record) => record.downloadId === downloadId);
}

/** Triggers the queue cleaner until the download's strike count reaches the target. */
async function strikeUntil(api: CleanuparrApi, downloadId: string, target: number): Promise<void> {
  await expect
    .poll(
      async () => {
        await api.jobs.trigger('QueueCleaner');
        return totalStrikes(api, downloadId);
      },
      { timeout: STRIKE_TIMEOUT, intervals: [2_000] },
    )
    .toBeGreaterThanOrEqual(target);
}

async function waitForRemoval(arr: LiveArr, downloadId: string): Promise<void> {
  await expect.poll(() => isQueued(arr, downloadId), { timeout: STRIKE_TIMEOUT, intervals: [2_000] }).toBe(false);
}

/**
 * Grabs a release from the arr's interactive search.
 * Same as a user clicking "grab" on a release the automatic search skipped.
 */
async function manualGrab(arr: LiveArr, queryParam: 'episodeId' | 'movieId', itemId: number, guid: string): Promise<void> {
  const releases = await arr.get<Array<Record<string, unknown>>>(`/api/v3/release?${queryParam}=${itemId}`);
  const release = releases.find((candidate) => candidate.guid === guid);

  expect(release, `release ${guid} missing from the ${queryParam}=${itemId} search`).toBeTruthy();
  await arr.post('/api/v3/release', release);
}

const createdArrInstances: Array<{ type: ArrType; id: string }> = [];

/** This spec's own rules would conflict with a sibling spec's completion-range stall rule. */
async function clearStallRules(api: CleanuparrApi): Promise<void> {
  const stallRules = await (await api.queueCleaner.listRules('stall')).json();
  for (const rule of stallRules) {
    await api.queueCleaner.deleteRule('stall', rule.id);
  }
}

test.describe('QueueCleaner: content type scoping (#381)', () => {
  test.beforeEach(async ({ api }) => {
    await resetLiveArrState();
    await clearStallRules(api);
  });

  test.afterEach(async ({ api }) => {
    for (const instance of createdArrInstances.splice(0)) {
      await api.arr.deleteInstance(instance.type, instance.id);
    }

    await clearStallRules(api);
    await teardownQueueCleaner(api);
    await resetLiveArrState();
  });

  for (const testCase of [SONARR_CASE, RADARR_CASE]) {
    test(`${testCase.label}: a New rule and an Upgrade rule strike their own download`, async ({ api }) => {
      test.setTimeout(300_000);

      const { downloadIds, mappings } = grabbableReleases(
        testCase.searchMode,
        [testCase.newTitle, testCase.upgradeTitle],
        testCase.category,
      );
      const [newDownloadId, upgradeDownloadId] = downloadIds;
      await indexerMock.stubMany(mappings);

      const instance = await (
        await api.arr.createInstance(testCase.arrType, {
          name: `e2e-content-type-${testCase.label}`,
          url: testCase.url,
          apiKey: testCase.apiKey,
          version: testCase.version,
          enabled: true,
        })
      ).json();
      createdArrInstances.push({ type: testCase.arrType, id: instance.id });

      await arrangeQueueCleaner(api);

      const newRule = await (
        await api.queueCleaner.createRule('stall', {
          name: `e2e-new-${testCase.label}`,
          enabled: true,
          maxStrikes: NEW_MAX_STRIKES,
          privacyType: 'Both',
          contentType: 'New',
          minCompletionPercentage: 0,
          maxCompletionPercentage: 100,
          deletePrivateTorrentsFromClient: true,
          changeCategory: false,
          resetStrikesOnProgress: false,
          minimumProgress: null,
        })
      ).json();
      expect(newRule.id, 'the New rule should have been created').toBeTruthy();

      const upgradeRule = await (
        await api.queueCleaner.createRule('stall', {
          name: `e2e-upgrade-${testCase.label}`,
          enabled: true,
          maxStrikes: UPGRADE_MAX_STRIKES,
          privacyType: 'Both',
          contentType: 'Upgrade',
          minCompletionPercentage: 0,
          maxCompletionPercentage: 100,
          deletePrivateTorrentsFromClient: true,
          changeCategory: false,
          resetStrikesOnProgress: false,
          minimumProgress: null,
        })
      ).json();
      expect(upgradeRule.id, 'the Upgrade rule should have been created').toBeTruthy();

      await manualGrab(testCase.arr, testCase.queryParam, testCase.newItemId, testCase.newTitle);
      await manualGrab(testCase.arr, testCase.queryParam, testCase.upgradeItemId, testCase.upgradeTitle);

      await waitForArrQueue(testCase.arr, newDownloadId, 90_000);
      await waitForArrQueue(testCase.arr, upgradeDownloadId, 90_000);

      await strikeUntil(api, newDownloadId, NEW_MAX_STRIKES);
      await waitForRemoval(testCase.arr, newDownloadId);

      // The Upgrade rule's ceiling is higher.
      // The runs that removed the new download must leave the upgrade queued.
      expect(await totalStrikes(api, upgradeDownloadId)).toBeLessThan(UPGRADE_MAX_STRIKES);
      expect(await isQueued(testCase.arr, upgradeDownloadId)).toBe(true);

      await strikeUntil(api, upgradeDownloadId, UPGRADE_MAX_STRIKES);
      await waitForRemoval(testCase.arr, upgradeDownloadId);
    });
  }
});

import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { indexerMock } from '../helpers/live-arr';
import {
  CHAPTARR,
  LIDARR,
  RADARR,
  SONARR,
  WHISPARR,
  WHISPARR2,
  TRANSITION_TIMEOUT,
  TRIGGERED_SEARCH_TIMEOUT,
  arrangeInstance,
  firstSearchEvent,
  resetLiveArrState,
  teardownInstances,
  triggerSeeker,
  waitForArrQueue,
} from '../helpers/seeker-live';
import { grabbableRelease } from '../helpers/mocks/torznab-stubs';
import { appLogsSince } from '../helpers/test-lifecycle';

/**
 * The same grab, driven by an on-demand trigger instead of the cron schedule.
 *
 * This is the canary for the patched image.
 * If the trigger is rejected the whole folder fails immediately, not by timeout.
 */

/**
 * What the monitor logs when it cannot read the arr's command list.
 *
 * It then polls each command on its own and reaches the same event status,
 * so the log is the only place a broken list request shows up.
 */
const LIST_FALLBACK = 'Failed to list commands on';

const TEST_TIMEOUT = TRIGGERED_SEARCH_TIMEOUT + TRANSITION_TIMEOUT + 120_000;

const AUDIOBOOK_SKIP_TAG = 'e2e-grab-skip-audiobook';

async function setChaptarrAudiobookTags(audiobookTags: number[]): Promise<void> {
  const author = await CHAPTARR.arr.get<Record<string, unknown>>(`/api/v1/author/${CHAPTARR.itemId}`);
  await CHAPTARR.arr.put(`/api/v1/author/${CHAPTARR.itemId}`, { ...author, audiobookTags });
}

const yearSceneIds = TEST_CONFIG.whisparr2Seed.yearSearch.scenes.map((scene) => scene.id);

test.describe('Seeker grab on demand', () => {
  test.beforeEach(async () => {
    await resetLiveArrState();
  });

  test.afterEach(async ({ api }) => {
    await teardownInstances(api);
    await setChaptarrAudiobookTags([]);
    await WHISPARR2.arr.put('/api/v3/episode/monitor', { episodeIds: yearSceneIds, monitored: true });
    await resetLiveArrState();
  });

  for (const target of [SONARR, RADARR, WHISPARR, WHISPARR2, LIDARR, CHAPTARR]) {
    test(`records the real ${target.type} v${target.version} grab on the search event`, async ({ api }) => {
      test.setTimeout(TEST_TIMEOUT);

      const startedAt = new Date().toISOString();
      const release = grabbableRelease(target.searchMode, target.release, target.category);
      await indexerMock.stubMany(release.mappings);

      if (target === WHISPARR2) {
        // Seeker picks a random year; the release only matches 2018
        await WHISPARR2.arr.put('/api/v3/episode/monitor', { episodeIds: yearSceneIds, monitored: false });
      }

      if (target === CHAPTARR) {
        // The release is an EPUB, so only the ebook row can grab it
        const tagId = await CHAPTARR.arr.ensureTag(AUDIOBOOK_SKIP_TAG);
        await setChaptarrAudiobookTags([tagId]);
      }

      const instanceId = await arrangeInstance(
        api,
        target,
        target === CHAPTARR ? { instance: { skipTags: [AUDIOBOOK_SKIP_TAG] } } : {},
      );
      await triggerSeeker(api);

      await waitForArrQueue(target.arr, release.downloadId, TRIGGERED_SEARCH_TIMEOUT);

      await expect
        .poll(async () => (await firstSearchEvent(api, instanceId))?.searchStatus, { timeout: TRANSITION_TIMEOUT })
        .toBe('Completed');

      const event = await firstSearchEvent(api, instanceId);
      expect(event?.grabbedItems).toContain(release.title);

      const logs = appLogsSince(startedAt);
      expect(logs, `the monitor could not read the ${target.type} command list`).not.toContain(LIST_FALLBACK);
    });
  }
});

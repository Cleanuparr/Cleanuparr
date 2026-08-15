import { test, expect } from '../fixtures/base';
import { indexerMock } from '../helpers/live-arr';
import {
  RADARR,
  arrangeInstance,
  firstSearchEvent,
  resetLiveArrState,
  teardownInstances,
  triggerSeeker,
} from '../helpers/seeker-live';
import { torznabSearchStub } from '../helpers/mocks/torznab-stubs';

/**
 * What happens to a search the arr never finishes.
 *
 * The indexer holds its answer, so Radarr's search command stays running.
 * The patched image cuts the command timeout to 20s, see e2e/patches.
 * The monitor checks the arr once more before it gives up, and the command is
 * still running, so the event settles on TimedOut.
 */

/** Longer than the patched timeout and the monitor's one minute poll put together. */
const INDEXER_DELAY_MS = 180_000;

const TEST_TIMEOUT = 300_000;
const TIMEOUT_WAIT = 240_000;

test.describe('Seeker search command timeout', () => {
  test.beforeEach(async () => {
    await resetLiveArrState();
    await indexerMock.stub(torznabSearchStub(RADARR.searchMode, [], INDEXER_DELAY_MS));
  });

  test.afterEach(async ({ api }) => {
    await teardownInstances(api);
    await resetLiveArrState();
  });

  test('times out a search the arr never finishes', async ({ api }) => {
    test.setTimeout(TEST_TIMEOUT);

    const instanceId = await arrangeInstance(api, RADARR);
    await triggerSeeker(api);

    await expect
      .poll(async () => (await firstSearchEvent(api, instanceId))?.searchStatus, { timeout: TIMEOUT_WAIT })
      .toBe('TimedOut');
  });
});

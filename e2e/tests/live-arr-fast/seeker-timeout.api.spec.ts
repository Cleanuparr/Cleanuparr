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
 * The patched image cuts the command timeout to 20s and the poll to 5s, see e2e/patches.
 * The monitor checks the arr once more before it gives up, and the command is
 * still running, so the event settles on TimedOut.
 */

/**
 * Long enough to outlive the patched 20s timeout and its 5s poll.
 *
 * Short enough that Radarr gets its answer: an indexer that times out on Radarr
 * lands in its backoff, and the next spec then searches with no active indexer.
 */
const INDEXER_DELAY_MS = 45_000;

const TEST_TIMEOUT = 180_000;
const TIMEOUT_WAIT = 90_000;

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

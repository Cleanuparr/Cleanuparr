import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { liveLazyLibrarian } from '../helpers/live-lazylibrarian';

/**
 * The connection test against a real LazyLibrarian, on the shipped image.
 *
 * LazyLibrarian answers HTTP 200 for every API failure, a wrong api key included.
 * The status code alone proves nothing.
 * Cleanuparr has to read the body to tell a good key from a bad one.
 *
 * The endpoint answers 200 for a working connection and 400 for a failed one.
 */

/** A well formed key that LazyLibrarian was never seeded with. */
const WRONG_API_KEY = 'ffffffffffffffffffffffffffffffff';

test.describe('LazyLibrarian test connection', () => {
  test.beforeAll(async () => {
    await liveLazyLibrarian.waitReady();
  });

  test('the seeded api key passes the connection test', async ({ api }) => {
    const res = await api.arr.testInstance('lazylibrarian', {
      name: 'live-lazylibrarian conn',
      url: TEST_CONFIG.liveLazyLibrarian.url,
      apiKey: TEST_CONFIG.liveLazyLibrarian.apiKey,
      version: 1,
    });

    expect(res.ok, await res.text()).toBe(true);
  });

  test('a wrong api key fails the connection test', async ({ api }) => {
    const res = await api.arr.testInstance('lazylibrarian', {
      name: 'live-lazylibrarian conn bad',
      url: TEST_CONFIG.liveLazyLibrarian.url,
      apiKey: WRONG_API_KEY,
      version: 1,
    });

    expect(res.status, 'a 200 carrying an error body is still a failed connection').toBe(400);
  });
});

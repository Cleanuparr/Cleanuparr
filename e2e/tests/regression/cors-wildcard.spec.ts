import { test, expect, TEST_CONFIG } from '../fixtures/base';

// Regression for GHSA-rwpc-36mg-fpvf — the API must not reflect arbitrary Origin
// headers in Access-Control-Allow-Origin (no wildcard CORS).

test.describe.serial('GHSA-rwpc-36mg-fpvf regression', () => {
  const ATTACKER_ORIGIN = 'https://attacker.example';

  test('does not reflect a malicious Origin in Access-Control-Allow-Origin on actual requests', async ({ request }) => {
    const res = await request.get(`${TEST_CONFIG.appUrl}/api/auth/status`, {
      headers: { Origin: ATTACKER_ORIGIN },
    });
    expect(res.status()).toBe(200);
    const acao = res.headers()['access-control-allow-origin'];
    expect(acao).toBeUndefined();
  });

  test('does not reflect on CORS preflight either', async ({ request }) => {
    const res = await request.fetch(`${TEST_CONFIG.appUrl}/api/auth/status`, {
      method: 'OPTIONS',
      headers: {
        Origin: ATTACKER_ORIGIN,
        'Access-Control-Request-Method': 'GET',
      },
    });
    const acao = res.headers()['access-control-allow-origin'];
    expect(acao).toBeUndefined();
  });
});

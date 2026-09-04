import { test, expect } from '../fixtures/base';

test.describe('Core — stats v2', () => {
  test('GET /api/v2/stats returns every section', async ({ api }) => {
    const res = await api.stats.get();
    expect(res.status).toBe(200);
    const body = await res.json();
    for (const section of ['events', 'strikes', 'removals', 'cleaned', 'searches', 'jobs', 'health']) {
      expect(body).toHaveProperty(section);
    }
    expect(body.timeframeHours).toBe(168);
    expect(typeof body.generatedAt).toBe('string');
  });

  test('GET echoes the requested timeframe and clamps it', async ({ api }) => {
    const inRange = await (await api.stats.get({ hours: 24 })).json();
    expect(inRange.timeframeHours).toBe(24);

    const tooLow = await (await api.stats.get({ hours: 0 })).json();
    expect(tooLow.timeframeHours).toBe(1);

    const tooHigh = await (await api.stats.get({ hours: 100_000 })).json();
    expect(tooHigh.timeframeHours).toBe(8760);
  });

  test('GET breaks events down by type and severity', async ({ api }) => {
    const body = await (await api.stats.get({ hours: 720 })).json();
    expect(typeof body.events.total).toBe('number');
    expect(typeof body.events.byType).toBe('object');
    expect(typeof body.events.bySeverity).toBe('object');
    expect(body.events.total).toBe(
      Object.values(body.events.byType as Record<string, number>).reduce((sum, n) => sum + n, 0),
    );
  });

  test('GET reports health for clients and arr instances', async ({ api }) => {
    const body = await (await api.stats.get()).json();
    expect(Array.isArray(body.health.downloadClients)).toBe(true);
    expect(Array.isArray(body.health.arrInstances)).toBe(true);
  });

  test('GET /api/v2/stats/timeline returns buckets for a metric', async ({ api }) => {
    const res = await api.stats.timeline({ metric: 'events', hours: 24, bucket: 'hour' });
    expect(res.status).toBe(200);
    const buckets = await res.json();
    expect(Array.isArray(buckets)).toBe(true);
    for (const bucket of buckets) {
      expect(typeof bucket.date).toBe('string');
      expect(typeof bucket.count).toBe('number');
    }
  });

  test('GET timeline rejects an unsupported bucket size', async ({ api }) => {
    const res = await api.stats.timeline({ metric: 'events', bucket: 'fortnight' });
    expect(res.status).toBe(400);
  });

  test('GET requires auth', async ({ anonymousApi }) => {
    const res = await anonymousApi.stats.get();
    expect(res.status).toBe(401);

    const timeline = await anonymousApi.stats.timeline();
    expect(timeline.status).toBe(401);
  });
});

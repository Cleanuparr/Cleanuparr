import { test, expect } from '@playwright/test';
import {
  loginAndGetToken,
  createDownloadClient,
  deleteDownloadClient,
  getDeadTorrentConfig,
  updateDeadTorrentConfig,
  getDownloadCleanerConfig,
} from '../helpers/app-api';

test.describe.serial('Dead Torrent Config API', () => {
  let token: string;
  let clientId: string;
  let rtorrentClientId: string;

  test.beforeAll(async () => {
    token = await loginAndGetToken();

    const res = await createDownloadClient(token, {
      enabled: false,
      name: 'e2e-dead-qbit',
      typeName: 'qBittorrent',
      type: 'Torrent',
      host: 'http://localhost:9999',
    });
    expect(res.status).toBe(201);
    clientId = (await res.json()).id;

    const rt = await createDownloadClient(token, {
      enabled: false,
      name: 'e2e-dead-rtorrent',
      typeName: 'rTorrent',
      type: 'Torrent',
      host: 'http://localhost:9998',
    });
    expect(rt.status).toBe(201);
    rtorrentClientId = (await rt.json()).id;
  });

  test.afterAll(async () => {
    if (clientId) {
      await deleteDownloadClient(token, clientId);
    }
    if (rtorrentClientId) {
      await deleteDownloadClient(token, rtorrentClientId);
    }
  });

  test('returns no config for a new client', async () => {
    const res = await getDeadTorrentConfig(token, clientId);
    // No config yet: 204 No Content, or 200 with null/empty body.
    expect([200, 204]).toContain(res.status);
    const body = await res.text();
    expect(body === '' || body === 'null').toBe(true);
  });

  test('creates and round-trips a valid config', async () => {
    const update = await updateDeadTorrentConfig(token, clientId, {
      enabled: true,
      targetCategory: 'cleanuparr-dead',
      useTag: true,
      maxStrikes: 5,
      categories: ['movies', 'tv'],
    });
    expect(update.status).toBe(200);

    const res = await getDeadTorrentConfig(token, clientId);
    const config = await res.json();
    expect(config.enabled).toBe(true);
    expect(config.targetCategory).toBe('cleanuparr-dead');
    expect(config.useTag).toBe(true);
    expect(config.maxStrikes).toBe(5);
    expect(config.categories).toEqual(['movies', 'tv']);
  });

  test('surfaces the config in the download cleaner config', async () => {
    const res = await getDownloadCleanerConfig(token);
    const body = await res.json();
    const client = body.clients.find((c: { downloadClientId: string }) => c.downloadClientId === clientId);
    expect(client).toBeDefined();
    expect(client.deadTorrentConfig).not.toBeNull();
    expect(client.deadTorrentConfig.maxStrikes).toBe(5);
    expect(client.deadTorrentConfig.categories).toEqual(['movies', 'tv']);
  });

  test('rejects strikes below the minimum of 3', async () => {
    const res = await updateDeadTorrentConfig(token, clientId, {
      enabled: true,
      targetCategory: 'cleanuparr-dead',
      useTag: false,
      maxStrikes: 2,
      categories: ['movies'],
    });
    expect(res.status).toBe(400);
  });

  test('rejects the target category being one of the source categories', async () => {
    const res = await updateDeadTorrentConfig(token, clientId, {
      enabled: true,
      targetCategory: 'movies',
      useTag: false,
      maxStrikes: 3,
      categories: ['movies'],
    });
    expect(res.status).toBe(400);
  });

  test('rejects empty categories when enabled', async () => {
    const res = await updateDeadTorrentConfig(token, clientId, {
      enabled: true,
      targetCategory: 'cleanuparr-dead',
      useTag: false,
      maxStrikes: 3,
      categories: [],
    });
    expect(res.status).toBe(400);
  });

  test('allows disabling regardless of other fields', async () => {
    const res = await updateDeadTorrentConfig(token, clientId, {
      enabled: false,
      targetCategory: '',
      useTag: false,
      maxStrikes: 0,
      categories: [],
    });
    expect(res.status).toBe(200);
  });

  test('rejects enabling for rTorrent', async () => {
    const res = await updateDeadTorrentConfig(token, rtorrentClientId, {
      enabled: true,
      targetCategory: 'cleanuparr-dead',
      useTag: false,
      maxStrikes: 3,
      categories: ['movies'],
    });
    expect(res.status).toBe(400);
  });
});

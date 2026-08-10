import { test, expect } from '@playwright/test';
import { loginAndGetToken, testDownloadClient } from '../helpers/app-api';
import { DelugeDriver } from '../helpers/torrent-clients/deluge';

const deluge = new DelugeDriver();

function payload(): Record<string, unknown> {
  return {
    enabled: true,
    name: 'Deluge reconnect e2e',
    typeName: deluge.typeName,
    type: 'Torrent',
    host: deluge.cleanuparrHost,
    username: deluge.username,
    password: deluge.password,
  };
}

/**
 * A Deluge Web UI that is not connected to its daemon.
 *
 * The Web UI is in this state after a daemon restart. A new installation also
 * starts in this state. The client must call `web.get_hosts` and `web.connect`
 * before a `core.*` call operates.
 *
 * `web.get_hosts` sends one row of values for each host. The third value is the
 * daemon port, and it is a JSON number:
 *
 *   {"result": [["a0de…", "127.0.0.1", 58846, "localclient"]], "error": null, "id": 1}
 *
 * A host row is not a list of strings.
 */
test.describe.serial('Deluge reconnect', () => {
  let token: string;

  test.beforeAll(async () => {
    token = await loginAndGetToken();
    await deluge.ready();
  });

  test.afterAll(async () => {
    // Connect the Web UI to the daemon again for the specs that follow.
    await deluge.ready().catch(() => {});
  });

  test('connects while the Web UI is already attached to the daemon', async () => {
    expect(await deluge.isWebUiConnected()).toBe(true);

    const res = await testDownloadClient(token, payload());
    expect(res.ok, `test connection failed: ${res.status} ${await res.text()}`).toBe(true);
  });

  test('reattaches the Web UI to the daemon when it is detached', async () => {
    test.setTimeout(120_000);

    await deluge.disconnectWebUi();
    expect(await deluge.isWebUiConnected()).toBe(false);

    const res = await testDownloadClient(token, payload());
    expect(res.ok, `test connection failed: ${res.status} ${await res.text()}`).toBe(true);
  });
});

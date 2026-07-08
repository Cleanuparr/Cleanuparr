import { execSync } from 'node:child_process';
import * as fs from 'node:fs';
import * as path from 'node:path';
import {
  CleanuparrApi,
  ensureAdminAccount,
  waitForApp,
} from './api';
import { TEST_CONFIG } from './test-config';

export interface RestartOptions {
  /**
   * Configure OIDC after admin setup. Required by tests under `tests/oidc/`
   * and `tests/account/oidc-config.api.spec.ts`. Defaults to `false` to keep
   * setup minimal for folders that don't touch OIDC.
   */
  configureOidc?: boolean;
}

const COMPOSE_FILE = 'docker-compose.e2e.yml';
const AUTH_FILE = path.resolve(process.cwd(), 'playwright/.auth/admin.json');

function restartAppContainer(): void {
  execSync(`docker compose -f ${COMPOSE_FILE} restart app`, {
    stdio: 'inherit',
    env: process.env,
  });
}

async function configureOidcForLegacyTests(api: CleanuparrApi): Promise<void> {
  await api.account.patchOidcConfig({
    enabled: true,
    issuerUrl: `${TEST_CONFIG.keycloakUrl}/realms/${TEST_CONFIG.realm}`,
    clientId: TEST_CONFIG.clientId,
    clientSecret: TEST_CONFIG.clientSecret,
    scopes: 'openid profile email',
    providerName: TEST_CONFIG.oidcProviderName,
  });
}

/**
 * Restart the app container, re-bootstrap the admin account, and persist the
 * resulting tokens to disk so subsequent tests in this folder can pick them
 * up via the {@link adminTokens} helper.
 *
 * Called once at the start of every spec folder via the matching
 * `tests/_setup/<folder>.setup.ts` file.
 */
export async function restartAppAndWait(opts: RestartOptions = {}): Promise<void> {
  restartAppContainer();

  const api = new CleanuparrApi();
  await waitForApp(api.client);
  await ensureAdminAccount(api);

  const tokens = await api.auth.loginAndCaptureTokens(
    TEST_CONFIG.adminUsername,
    TEST_CONFIG.adminPassword,
  );

  api.setToken(tokens.accessToken);
  if (opts.configureOidc) {
    await configureOidcForLegacyTests(api);
  }

  fs.mkdirSync(path.dirname(AUTH_FILE), { recursive: true });
  fs.writeFileSync(AUTH_FILE, JSON.stringify(tokens, null, 2));
}

/**
 * Loads the admin tokens written by the most recent folder setup.
 * Throws if no setup has run (i.e. the spec was executed without its
 * `setup:*` project dependency).
 */
export function adminTokens(): { accessToken: string; refreshToken: string; expiresIn: number } {
  if (!fs.existsSync(AUTH_FILE)) {
    throw new Error(
      `Admin tokens file not found at ${AUTH_FILE}.\n` +
        `Each spec folder must be run with its matching setup:<folder> project; ` +
        `the setup project writes this file before tests run.`,
    );
  }
  return JSON.parse(fs.readFileSync(AUTH_FILE, 'utf-8'));
}

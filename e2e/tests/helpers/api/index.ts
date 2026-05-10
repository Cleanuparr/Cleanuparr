import { TEST_CONFIG } from '../test-config';
import { ApiClient, ApiClientOptions, waitForApp } from './client';
import { AccountApi } from './account';
import { ArrApi } from './arr';
import { AuthApi } from './auth';
import { BlacklistSyncApi } from './blacklist-sync';
import { DownloadCleanerApi } from './download-cleaner';
import { DownloadClientApi } from './download-client';
import { EventsApi, ManualEventsApi, StatsApi, StrikesApi } from './events';
import { GeneralApi } from './general';
import { HealthApi, StatusApi } from './status';
import { JobsApi } from './jobs';
import { MalwareBlockerApi } from './malware-blocker';
import { NotificationsApi } from './notifications';
import { QueueCleanerApi } from './queue-cleaner';
import { SeekerApi } from './seeker';
import { TestResetApi } from './test-reset';

/**
 * One-stop API surface for e2e tests. Use the Playwright fixtures
 * in `tests/fixtures/base.ts` instead of constructing this directly.
 */
export class CleanuparrApi {
  readonly client: ApiClient;
  readonly auth: AuthApi;
  readonly account: AccountApi;
  readonly arr: ArrApi;
  readonly downloadClient: DownloadClientApi;
  readonly notifications: NotificationsApi;
  readonly general: GeneralApi;
  readonly queueCleaner: QueueCleanerApi;
  readonly downloadCleaner: DownloadCleanerApi;
  readonly malwareBlocker: MalwareBlockerApi;
  readonly blacklistSync: BlacklistSyncApi;
  readonly seeker: SeekerApi;
  readonly jobs: JobsApi;
  readonly events: EventsApi;
  readonly manualEvents: ManualEventsApi;
  readonly strikes: StrikesApi;
  readonly stats: StatsApi;
  readonly status: StatusApi;
  readonly health: HealthApi;
  readonly testReset: TestResetApi;

  constructor(opts: ApiClientOptions = {}) {
    this.client = new ApiClient(opts);
    this.auth = new AuthApi(this.client);
    this.account = new AccountApi(this.client);
    this.arr = new ArrApi(this.client);
    this.downloadClient = new DownloadClientApi(this.client);
    this.notifications = new NotificationsApi(this.client);
    this.general = new GeneralApi(this.client);
    this.queueCleaner = new QueueCleanerApi(this.client);
    this.downloadCleaner = new DownloadCleanerApi(this.client);
    this.malwareBlocker = new MalwareBlockerApi(this.client);
    this.blacklistSync = new BlacklistSyncApi(this.client);
    this.seeker = new SeekerApi(this.client);
    this.jobs = new JobsApi(this.client);
    this.events = new EventsApi(this.client);
    this.manualEvents = new ManualEventsApi(this.client);
    this.strikes = new StrikesApi(this.client);
    this.stats = new StatsApi(this.client);
    this.status = new StatusApi(this.client);
    this.health = new HealthApi(this.client);
    this.testReset = new TestResetApi(this.client);
  }

  setToken(token: string | undefined): void {
    this.client.setToken(token);
  }
}

export async function adminLogin(
  api: CleanuparrApi,
  username = TEST_CONFIG.adminUsername,
  password = TEST_CONFIG.adminPassword,
): Promise<void> {
  const tokens = await api.auth.loginAndCaptureTokens(username, password);
  api.setToken(tokens.accessToken);
}

export async function ensureAdminAccount(api: CleanuparrApi): Promise<void> {
  const create = await api.auth.setupAccount(TEST_CONFIG.adminUsername, TEST_CONFIG.adminPassword);
  if (!create.ok && create.status !== 409 && create.status !== 403) {
    throw new Error(`Failed to create admin account: ${create.status} ${await create.text()}`);
  }
  const complete = await api.auth.setupComplete();
  if (!complete.ok && complete.status !== 409 && complete.status !== 403) {
    throw new Error(`Failed to complete setup: ${complete.status} ${await complete.text()}`);
  }
}

export { ApiClient, waitForApp };
export * from './auth';
export * from './account';
export * from './arr';
export * from './download-client';
export * from './notifications';
export * from './general';
export * from './queue-cleaner';
export * from './download-cleaner';
export * from './malware-blocker';
export * from './blacklist-sync';
export * from './seeker';
export * from './jobs';
export * from './events';
export * from './status';
export * from './test-reset';

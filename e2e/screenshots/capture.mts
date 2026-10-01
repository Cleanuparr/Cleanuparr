// Docs screenshot capture pipeline. Run via run.sh, or directly:
//   BASE_URL=http://localhost:11092 OUT_DIR=/tmp/shots node capture.mts [filter...]
//
// Talks to a real running Cleanuparr instance: creates the admin account over
// the API, logs in, then drives Playwright/Chromium to capture each shot.
// The SignalR app hub websocket is intercepted so the logs page/widget show
// fabricated data instead of whatever noise a demo instance produces.

import { chromium } from 'playwright-core';
import type { Browser, BrowserContext, Page, WebSocketRoute } from 'playwright-core';
import { mkdirSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));

const BASE_URL = process.env.BASE_URL ?? 'http://localhost:11092';
const OUT_DIR = resolve(process.env.OUT_DIR ?? resolve(__dirname, '../../docs/static/img/screenshots'));
const USERNAME = 'admin';
const PASSWORD = 'Cleanuparr123!';
const FILTERS = process.argv.slice(2);

const ACCESS_TOKEN_KEY = 'access_token';
const REFRESH_TOKEN_KEY = 'refresh_token';
const THEME_KEY = 'cleanuparr-theme';
const ACCENT_KEY = 'cleanuparr-accent';
const CUSTOM_ACCENT_KEY = 'cleanuparr-custom-accent';
const CYAN = '#06b6d4';

type Theme = 'dark' | 'light';
type Accent = 'default' | 'custom';

interface Shot {
  file: string;
  route: string;
  theme: Theme;
  accent: Accent;
  /** Scroll `.shell__content` so the "Recent Logs"/"Recent Events" row sits near the top. */
  scrollToLogsRow?: boolean;
  mobile?: boolean;
  /** Composite shot: capture each route as a phone screen, then stitch them side by side. */
  mobileRoutes?: string[];
}

const SHOTS: Shot[] = [
  { file: 'dashboard.png', route: '/dashboard', theme: 'dark', accent: 'default' },
  { file: 'dashboard2.png', route: '/dashboard', theme: 'light', accent: 'custom', scrollToLogsRow: true },
  { file: 'download_clients.png', route: '/settings/download-clients', theme: 'light', accent: 'default' },
  { file: 'notifications.png', route: '/settings/notifications', theme: 'dark', accent: 'custom' },
  { file: 'events.png', route: '/events', theme: 'light', accent: 'custom' },
  { file: 'logs.png', route: '/logs', theme: 'dark', accent: 'default' },
  { file: 'seeker-stats_searches.png', route: '/seeker-stats?tab=searches', theme: 'dark', accent: 'custom' },
  { file: 'seeker-stats_quality.png', route: '/seeker-stats?tab=quality', theme: 'light', accent: 'default' },
  { file: 'seeker-stats_upgrades.png', route: '/seeker-stats?tab=upgrades', theme: 'dark', accent: 'default' },
  { file: 'strikes.png', route: '/strikes', theme: 'light', accent: 'custom' },
  { file: 'settings_queue-cleaner.png', route: '/settings/queue-cleaner', theme: 'dark', accent: 'default' },
  { file: 'settings_seeker.png', route: '/settings/seeker', theme: 'light', accent: 'default' },
  { file: 'appearance.png', route: '/settings/appearance', theme: 'dark', accent: 'custom' },
  {
    file: 'mobile.png',
    route: '/dashboard',
    theme: 'dark',
    accent: 'default',
    mobile: true,
    mobileRoutes: ['/dashboard', '/events', '/logs'],
  },
];

async function ensureAuth(): Promise<{ accessToken: string; refreshToken: string }> {
  const createRes = await fetch(`${BASE_URL}/api/auth/setup/account`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ username: USERNAME, password: PASSWORD }),
  });
  if (createRes.status === 201) {
    const completeRes = await fetch(`${BASE_URL}/api/auth/setup/complete`, { method: 'POST' });
    if (!completeRes.ok) {
      throw new Error(`setup/complete failed: ${completeRes.status} ${await completeRes.text()}`);
    }
  } else if (createRes.status !== 409) {
    throw new Error(`setup/account failed: ${createRes.status} ${await createRes.text()}`);
  }

  const loginRes = await fetch(`${BASE_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ username: USERNAME, password: PASSWORD }),
  });
  if (!loginRes.ok) {
    throw new Error(`login failed: ${loginRes.status} ${await loginRes.text()}`);
  }
  const body: any = await loginRes.json();
  if (body.requiresTwoFactor || !body.tokens) {
    throw new Error('login unexpectedly requires 2FA on a fresh account');
  }
  return { accessToken: body.tokens.accessToken, refreshToken: body.tokens.refreshToken };
}

// ---- fabricated logs, built from the real seeded events/strikes ------
//
// Logs are synthesized from the instance's own data (fetched over the API
// with the admin token) rather than random title/instance combinations, so
// "Search triggered for X on Y" always pairs a title with the arr instance
// that actually owns it, using its real host and the real download client
// names/types that were seeded.

function pick<T>(arr: T[]): T {
  return arr[Math.floor(Math.random() * arr.length)];
}

function randInt(min: number, max: number): number {
  return min + Math.floor(Math.random() * (max - min + 1));
}

interface SeedEvent {
  timestamp: string;
  eventType: string;
  message: string;
  severity: string;
  arrInstanceId?: string | null;
  downloadClientId?: string | null;
  itemTitle?: string | null;
  strikeCount?: number | null;
  searchReason?: string | null;
  cleanReason?: string | null;
  deleteReason?: string | null;
}

interface InstanceInfo {
  name: string;
  url: string;
}

interface ClientInfo {
  name: string;
  typeName: string;
}

interface SeedContext {
  events: SeedEvent[];
  instances: Map<string, InstanceInfo>;
  clients: Map<string, ClientInfo>;
}

const ARR_TYPE_SLUGS = ['sonarr', 'radarr', 'lidarr', 'readarr', 'whisparr', 'sportarr', 'lazylibrarian'];

async function authedGet(path: string, accessToken: string): Promise<any | null> {
  const res = await fetch(`${BASE_URL}${path}`, {
    headers: { authorization: `Bearer ${accessToken}` },
  });
  if (!res.ok) {
    return null;
  }
  return res.json();
}

async function fetchSeedContext(accessToken: string): Promise<SeedContext> {
  const instances = new Map<string, InstanceInfo>();
  const clients = new Map<string, ClientInfo>();

  await Promise.all(
    ARR_TYPE_SLUGS.map(async (slug) => {
      const cfg = await authedGet(`/api/configuration/${slug}`, accessToken);
      for (const inst of cfg?.instances ?? []) {
        if (inst?.id) {
          instances.set(inst.id, { name: inst.name, url: inst.url });
        }
      }
    }),
  );

  const dc = await authedGet('/api/configuration/download_client', accessToken);
  for (const c of dc?.clients ?? []) {
    if (c?.id) {
      clients.set(c.id, { name: c.name, typeName: c.typeName });
    }
  }

  const eventsRes = await authedGet('/api/events?pageSize=100', accessToken);
  const events: SeedEvent[] = eventsRes?.items ?? [];

  return { events, instances, clients };
}

interface LogLine {
  timestamp: number;
  level: 'Information' | 'Warning' | 'Error';
  message: string;
  category: string;
  jobName?: string;
  instanceName?: string;
  downloadClientType?: string;
  downloadClientName?: string;
}

/** Mirrors the real per-EventType log line a job would have written, filled with real seeded data. */
function buildLogFromEvent(ev: SeedEvent, ctx: SeedContext): LogLine | null {
  const instance = ev.arrInstanceId ? ctx.instances.get(ev.arrInstanceId) : undefined;
  const client = ev.downloadClientId ? ctx.clients.get(ev.downloadClientId) : undefined;
  const title = ev.itemTitle ?? 'item';
  const timestamp = Date.parse(ev.timestamp);
  if (!Number.isFinite(timestamp)) {
    return null;
  }

  const base = {
    timestamp,
    instanceName: instance?.name,
    downloadClientName: client?.name,
    downloadClientType: client?.typeName?.toLowerCase(),
  };

  switch (ev.eventType) {
    case 'FailedImportStrike':
    case 'StalledStrike':
    case 'DownloadingMetadataStrike':
    case 'SlowSpeedStrike':
    case 'SlowTimeStrike':
    case 'DeadTorrentStrike':
      return {
        ...base,
        level: 'Warning',
        category: 'ItemStriker',
        jobName: 'QueueCleaner',
        message: `Item on strike number ${ev.strikeCount ?? 1} | reason ${ev.eventType.replace('Strike', '')} | ${title}`,
      };
    case 'QueueItemDeleted':
      return {
        ...base,
        level: 'Information',
        category: 'ItemStriker',
        jobName: 'QueueCleaner',
        message: `Removing item with max strikes | reason ${ev.deleteReason ?? 'Stalled'} | ${title}`,
      };
    case 'DownloadCleaned':
      return {
        ...base,
        level: 'Information',
        category: 'DownloadCleaner',
        jobName: 'DownloadCleaner',
        message: `download cleaned | reason ${ev.cleanReason ?? 'MaxRatioReached'} | ${title}`,
      };
    case 'CategoryChanged':
      return {
        ...base,
        level: 'Information',
        category: 'DownloadCleaner',
        jobName: 'DownloadCleaner',
        message: `category changed | ${title}`,
      };
    case 'DownloadMarkedForDeletion':
      return {
        ...base,
        level: 'Information',
        category: 'GenericHandler',
        jobName: 'QueueCleaner',
        message: `item marked for removal | ${title}${instance ? ` | ${instance.url}` : ''}`,
      };
    case 'SearchTriggered':
      return {
        ...base,
        level: 'Information',
        category: 'Seeker',
        jobName: 'Seeker',
        message: `Search triggered for ${title} (${ev.searchReason ?? 'Missing'})${instance ? ` | ${instance.url}` : ''}`,
      };
    case 'StrikeReset':
      return {
        ...base,
        level: 'Information',
        category: 'ItemStriker',
        jobName: 'QueueCleaner',
        message: `Progress detected | resetting strikes | ${title}`,
      };
    case 'ForceImported':
      return {
        ...base,
        level: 'Information',
        category: 'Seeker',
        jobName: 'Seeker',
        message: `manual import forced | ${title}`,
      };
    case 'DownloadStopped':
      return {
        ...base,
        level: 'Information',
        category: 'DownloadClient',
        jobName: 'QueueCleaner',
        message: `download stopped | ${title}`,
      };
    default:
      return null;
  }
}

const GENERIC_FILLERS: Array<() => string> = [
  () => `Evaluating ${randInt(4, 30)} downloads for cleanup`,
  () => 'Finished cleanup evaluation',
  () => `Successfully loaded ${randInt(2, 5)} blocklists`,
  () => `Evaluating ${randInt(3, 15)} downloads for hardlinks`,
  () => 'Finished hardlinks evaluation',
];

function fabricateLogsFromSeed(ctx: SeedContext, count: number): Record<string, unknown>[] {
  const primary: LogLine[] = [];
  for (const ev of ctx.events) {
    const line = buildLogFromEvent(ev, ctx);
    if (line) {
      primary.push(line);
    }
  }
  primary.sort((a, b) => b.timestamp - a.timestamp);
  // Reserve a couple of slots so the injected warnings/errors below always survive the
  // final newest-`count` trim instead of racing real events for the last spots.
  const kept = primary.slice(0, Math.max(0, count - 2));

  const times = kept.map((l) => l.timestamp);
  const newest = times.length ? Math.max(...times) : Date.now();
  const oldest = times.length ? Math.min(...times) : Date.now() - 2 * 60 * 60 * 1000;
  const span = Math.max(newest - oldest, 5 * 60 * 1000);
  const fillerTimestamp = () => oldest + Math.random() * span;
  // Biased toward the newest slice of the window, so injected noise reliably survives trimming.
  const recentTimestamp = () => newest - Math.random() * span * 0.15;

  const clientNames = [...ctx.clients.values()].map((c) => c.name);
  const instanceNames = [...ctx.instances.values()].map((i) => i.name);

  // Pad with generic, title-free job status lines (these are coherent with no seeded data at all).
  const filler: LogLine[] = [];
  while (kept.length + filler.length < count) {
    const category = Math.random() < 0.5 ? 'QueueCleaner' : 'DownloadCleaner';
    filler.push({
      timestamp: fillerTimestamp(),
      level: 'Information',
      category,
      jobName: category,
      message: pick(GENERIC_FILLERS)(),
    });
  }

  // A light sprinkle of benign operational warnings (~1 in 8) and at most one or two
  // errors that read as normal transient noise, using real seeded instance/client names.
  const all = [...kept, ...filler];
  const targetWarnings = Math.max(1, Math.round(count / 8));
  let warnings = all.filter((l) => l.level === 'Warning').length;
  while (warnings < targetWarnings && all.length < count + 4 && (clientNames.length || instanceNames.length)) {
    if (clientNames.length && (Math.random() < 0.5 || instanceNames.length === 0)) {
      all.push({
        timestamp: recentTimestamp(),
        level: 'Warning',
        category: 'DownloadClient',
        jobName: 'QueueCleaner',
        message: `slow response from ${pick(clientNames)}, retrying`,
      });
    } else {
      all.push({
        timestamp: recentTimestamp(),
        level: 'Warning',
        category: 'Seeker',
        jobName: 'Seeker',
        message: `slow response from ${pick(instanceNames)} API, retrying`,
      });
    }
    warnings++;
  }

  if (clientNames.length) {
    all.push({
      timestamp: recentTimestamp(),
      level: 'Error',
      category: 'DownloadClient',
      jobName: 'QueueCleaner',
      message: `${pick(clientNames)} request timed out, retrying`,
    });
  }
  if (instanceNames.length && Math.random() < 0.4) {
    all.push({
      timestamp: recentTimestamp(),
      level: 'Error',
      category: 'Seeker',
      jobName: 'Seeker',
      message: `timed out contacting ${pick(instanceNames.filter((n) => /sonarr|radarr/i.test(n)))}, retrying`,
    });
  }

  all.sort((a, b) => a.timestamp - b.timestamp);
  const trimmed = all.length > count ? all.slice(all.length - count) : all;

  return trimmed.map((l) => ({
    timestamp: new Date(l.timestamp).toISOString(),
    level: l.level,
    message: l.message,
    category: l.category,
    jobName: l.jobName,
    instanceName: l.instanceName,
    downloadClientType: l.downloadClientType,
    downloadClientName: l.downloadClientName,
  }));
}

// ---- SignalR frame handling -------------------------------------------

const RECORD_SEPARATOR = '\x1e';

function transformHubFrames(raw: string, ctx: SeedContext): string | null {
  const parts = raw.split(RECORD_SEPARATOR).filter((p) => p.length > 0);
  if (parts.length === 0) {
    return null;
  }

  const outFrames: string[] = [];
  for (const part of parts) {
    let parsed: any;
    try {
      parsed = JSON.parse(part);
    } catch {
      // Not JSON: forward unchanged.
      outFrames.push(part);
      continue;
    }

    // Invocation message (type 1): { type: 1, target: string, arguments: [...] }
    if (parsed && parsed.type === 1 && parsed.target === 'LogsReceived') {
      parsed.arguments = [fabricateLogsFromSeed(ctx, 40)];
      outFrames.push(JSON.stringify(parsed));
      continue;
    }
    if (parsed && parsed.type === 1 && parsed.target === 'LogReceived') {
      // Drop real single-log pushes so the recent-logs widget stays fabricated.
      continue;
    }

    outFrames.push(JSON.stringify(parsed));
  }

  if (outFrames.length === 0) {
    return null;
  }
  return outFrames.map((f) => f + RECORD_SEPARATOR).join('');
}

function installHubInterceptor(page: Page, ctx: SeedContext): Promise<void> {
  return page.routeWebSocket(/\/api\/hubs\/app/, (ws: WebSocketRoute) => {
    const server = ws.connectToServer();
    ws.onMessage((message) => server.send(message));
    server.onMessage((message) => {
      const text = typeof message === 'string' ? message : message.toString('utf8');
      const transformed = transformHubFrames(text, ctx);
      if (transformed !== null) {
        ws.send(transformed);
      }
    });
  });
}

// ---- capture helpers ----------------------------------------------------

async function disableAnimations(page: Page): Promise<void> {
  await page.addInitScript(() => {
    const apply = () => {
      const style = document.createElement('style');
      style.textContent = `
        *, *::before, *::after {
          transition-duration: 0s !important;
          animation-duration: 0s !important;
          animation-delay: 0s !important;
          scroll-behavior: auto !important;
        }
      `;
      document.head.appendChild(style);
    };
    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', apply);
    } else {
      apply();
    }
  });
}

async function seedStorage(page: Page, tokens: { accessToken: string; refreshToken: string }, shot: Shot): Promise<void> {
  await page.goto(`${BASE_URL}/auth/login`, { waitUntil: 'domcontentloaded' });
  await page.evaluate(
    (args) => {
      localStorage.setItem(args.accessKey, args.accessToken);
      localStorage.setItem(args.refreshKey, args.refreshToken);
      localStorage.setItem(args.themeKey, args.theme);
      localStorage.setItem(args.accentKey, args.accent);
      localStorage.setItem(args.customAccentKey, args.customAccent);
    },
    {
      accessToken: tokens.accessToken,
      refreshToken: tokens.refreshToken,
      theme: shot.theme,
      accent: shot.accent,
      customAccent: CYAN,
      accessKey: ACCESS_TOKEN_KEY,
      refreshKey: REFRESH_TOKEN_KEY,
      themeKey: THEME_KEY,
      accentKey: ACCENT_KEY,
      customAccentKey: CUSTOM_ACCENT_KEY,
    },
  );
}

async function settle(page: Page): Promise<void> {
  await page.waitForLoadState('networkidle').catch(() => {});
  await page.waitForTimeout(800);
}

// The document never scrolls; `.shell__content` is the scroll container.
async function scrollToRecentLogsRow(page: Page): Promise<void> {
  await page.evaluate(() => {
    const container = document.querySelector('.shell__content') as HTMLElement | null;
    if (!container) {
      return;
    }
    const heading = [...container.querySelectorAll('h1, h2, h3, h4, h5, h6')].find((el) =>
      el.textContent?.includes('Recent Logs'),
    ) as HTMLElement | undefined;
    if (!heading) {
      return;
    }
    const containerRect = container.getBoundingClientRect();
    const headingRect = heading.getBoundingClientRect();
    const offset = headingRect.top - containerRect.top + container.scrollTop;
    container.scrollTop = Math.max(0, offset - 24);
  });
  await settle(page);
}

async function assertNoAuthLeak(page: Page, shot: Shot): Promise<void> {
  const url = page.url();
  if (url.includes('/auth/login') || url.includes('/auth/setup')) {
    throw new Error(`${shot.file}: landed on auth page (${url}) instead of the target route`);
  }
  const toastCount = await page.locator('[class*="toast"][class*="error"], .toast-error').count();
  if (toastCount > 0) {
    throw new Error(`${shot.file}: an error toast is visible`);
  }
}

async function prepareShotPage(
  context: BrowserContext,
  tokens: { accessToken: string; refreshToken: string },
  shot: Shot,
  seedCtx: SeedContext,
): Promise<Page> {
  const page = await context.newPage();
  await disableAnimations(page);
  await installHubInterceptor(page, seedCtx);
  await seedStorage(page, tokens, shot);
  await page.goto(`${BASE_URL}${shot.route}`, { waitUntil: 'domcontentloaded' });
  await settle(page);
  await assertNoAuthLeak(page, shot);
  return page;
}

async function captureShot(
  context: BrowserContext,
  tokens: { accessToken: string; refreshToken: string },
  shot: Shot,
  seedCtx: SeedContext,
): Promise<void> {
  const page = await prepareShotPage(context, tokens, shot, seedCtx);

  if (shot.scrollToLogsRow) {
    await scrollToRecentLogsRow(page);
  }

  const outPath = resolve(OUT_DIR, shot.file);
  await page.screenshot({ path: outPath, fullPage: false });
  await page.close();
  console.log(`  wrote ${shot.file}`);
}

/**
 * Captures each of `shot.mobileRoutes` as its own phone screen (reusing the
 * same per-shot auth/theme/log-fabrication setup as `captureShot`), then
 * composes them side by side into one landscape 3840x2160 image.
 */
async function captureMobileComposite(
  mobileContext: BrowserContext,
  desktopContext: BrowserContext,
  tokens: { accessToken: string; refreshToken: string },
  shot: Shot,
  seedCtx: SeedContext,
): Promise<void> {
  const routes = shot.mobileRoutes ?? [shot.route];
  const images: string[] = [];
  for (const route of routes) {
    const page = await prepareShotPage(mobileContext, tokens, { ...shot, route }, seedCtx);
    const buffer = await page.screenshot({ fullPage: false });
    images.push(`data:image/png;base64,${buffer.toString('base64')}`);
    await page.close();
  }

  const compositePage = await desktopContext.newPage();
  await compositePage.setContent(`
    <!doctype html>
    <html>
      <head>
        <style>
          html, body {
            margin: 0;
            width: 100vw;
            height: 100vh;
            background: #0b0714;
            display: flex;
            align-items: center;
            justify-content: center;
          }
          .row { display: flex; gap: 48px; align-items: center; justify-content: center; }
          .phone {
            height: 920px;
            border-radius: 36px;
            box-shadow: 0 20px 60px rgba(0, 0, 0, 0.5);
            display: block;
          }
        </style>
      </head>
      <body>
        <div class="row">
          ${images.map((src) => `<img class="phone" src="${src}" />`).join('\n          ')}
        </div>
      </body>
    </html>
  `);
  await compositePage.waitForTimeout(200); // let the data-url images decode before screenshotting

  const outPath = resolve(OUT_DIR, shot.file);
  await compositePage.screenshot({ path: outPath, fullPage: false });
  await compositePage.close();
  console.log(`  wrote ${shot.file}`);
}

async function main(): Promise<void> {
  mkdirSync(OUT_DIR, { recursive: true });

  const tokens = await ensureAuth();
  const seedCtx = await fetchSeedContext(tokens.accessToken);
  console.log(
    `seed context: ${seedCtx.events.length} events, ${seedCtx.instances.size} arr instances, ${seedCtx.clients.size} download clients`,
  );

  const shots = FILTERS.length > 0
    ? SHOTS.filter((s) => FILTERS.some((f) => s.file.includes(f) || s.route.includes(f)))
    : SHOTS;

  if (shots.length === 0) {
    console.error('No shots matched the given filter(s):', FILTERS.join(', '));
    process.exit(1);
  }

  const browser: Browser = await chromium.launch();

  const desktopContext = await browser.newContext({
    viewport: { width: 1920, height: 1080 },
    deviceScaleFactor: 2,
  });
  const mobileContext = await browser.newContext({
    viewport: { width: 390, height: 844 },
    deviceScaleFactor: 3,
    isMobile: true,
    hasTouch: true,
  });

  try {
    for (const shot of shots) {
      console.log(`capturing ${shot.file} (${shot.route}, ${shot.theme}/${shot.accent}${shot.mobile ? ', mobile' : ''})`);
      if (shot.mobileRoutes) {
        await captureMobileComposite(mobileContext, desktopContext, tokens, shot, seedCtx);
        continue;
      }
      const context = shot.mobile ? mobileContext : desktopContext;
      await captureShot(context, tokens, shot, seedCtx);
    }
  } finally {
    await desktopContext.close();
    await mobileContext.close();
    await browser.close();
  }

  console.log(`Done. ${shots.length} shot(s) written to ${OUT_DIR}`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});

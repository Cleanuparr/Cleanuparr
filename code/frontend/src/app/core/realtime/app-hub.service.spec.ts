const hub = vi.hoisted(() => {
  const handlers = new Map<string, (...args: unknown[]) => void>();
  const invocations: { method: string; args: unknown[] }[] = [];
  const lifecycle = new Map<string, () => void>();
  const built: {
    url: string | null;
    options: { accessTokenFactory?: () => Promise<string> } | null;
    retry: { nextRetryDelayInMilliseconds: (context: { previousRetryCount: number }) => number } | null;
    logLevel: unknown;
    count: number;
  } = { url: null, options: null, retry: null, logLevel: null, count: 0 };

  const connection = {
    state: 'Connected',
    on: (name: string, handler: (...args: unknown[]) => void): void => {
      handlers.set(name, handler);
    },
    onreconnecting: (callback: () => void): void => {
      lifecycle.set('reconnecting', callback);
    },
    onreconnected: (callback: () => void): void => {
      lifecycle.set('reconnected', callback);
    },
    onclose: (callback: () => void): void => {
      lifecycle.set('close', callback);
    },
    start: (): Promise<void> => (startError ? Promise.reject(startError) : Promise.resolve()),
    stop: (): Promise<void> => {
      stopped++;
      return Promise.resolve();
    },
    invoke: (method: string, ...args: unknown[]): Promise<void> => {
      invocations.push({ method, args });
      return Promise.resolve();
    },
  };

  let startError: unknown = null;
  let stopped = 0;

  return {
    handlers,
    invocations,
    lifecycle,
    built,
    connection,
    failNextStarts: (error: unknown): void => {
      startError = error;
    },
    stopCount: (): number => stopped,
    reset: (): void => {
      handlers.clear();
      invocations.length = 0;
      lifecycle.clear();
      built.url = null;
      built.options = null;
      built.retry = null;
      built.logLevel = null;
      built.count = 0;
      connection.state = 'Connected';
      startError = null;
      stopped = 0;
    },
  };
});

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: class {
    withUrl(url: string, options: { accessTokenFactory?: () => Promise<string> }) {
      hub.built.url = url;
      hub.built.options = options;
      return this;
    }
    withAutomaticReconnect(retry: {
      nextRetryDelayInMilliseconds: (context: { previousRetryCount: number }) => number;
    }) {
      hub.built.retry = retry;
      return this;
    }
    configureLogging(level: unknown) {
      hub.built.logLevel = level;
      return this;
    }
    build() {
      hub.built.count++;
      return hub.connection;
    }
  },
  LogLevel: { Warning: 'Warning' },
  HubConnectionState: { Connected: 'Connected', Disconnected: 'Disconnected' },
}));

import { TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import { AuthService, TokenResponse } from '@core/auth/auth.service';
import { AppEvent, ManualEvent } from '@core/models/event.models';
import { JobInfo } from '@core/models/job.models';
import { LogEntry } from '@core/models/signalr.models';
import { RecentStrike } from '@core/models/strike.models';
import { ApplicationPathService } from '@core/services/base-path.service';
import { AppHubService } from './app-hub.service';

const MAX_BUFFER = 1000;

describe('AppHubService', () => {
  interface SetupOptions {
    accessToken?: string | null;
    expired?: boolean;
    refreshResult?: TokenResponse | null;
  }

  function log(message: string): LogEntry {
    return { timestamp: new Date(0), level: 'Information', message };
  }

  function event(id: string, message = id): AppEvent {
    return {
      id,
      timestamp: new Date(0),
      eventType: 'StalledStrike',
      message,
      severity: 'Warning',
      isDryRun: false,
    };
  }

  function manualEvent(id: string, message = id): ManualEvent {
    return {
      id,
      timestamp: new Date(0),
      message,
      severity: 'Warning',
      isResolved: false,
      isDryRun: false,
    };
  }

  function strike(id: string, title = id): RecentStrike {
    return { id, type: 'Stalled', createdAt: '2026-07-31T12:00:00Z', downloadId: 'd', title, isDryRun: false };
  }

  function job(jobType: string, statusText = 'Idle'): JobInfo {
    return { name: jobType, status: statusText, schedule: '0 * * * * ?', jobType };
  }

  function emit(name: string, payload?: unknown): void {
    const handler = hub.handlers.get(name);
    if (!handler) {
      throw new Error(`no handler registered for ${name}`);
    }
    handler(payload);
  }

  function fire(name: string): void {
    const callback = hub.lifecycle.get(name);
    if (!callback) {
      throw new Error(`no lifecycle callback registered for ${name}`);
    }
    callback();
  }

  function methods(): string[] {
    return hub.invocations.map((invocation) => invocation.method);
  }

  function setup(options: SetupOptions = {}) {
    const refreshCalls: number[] = [];

    const auth = {
      getAccessToken: (): string | null => options.accessToken ?? null,
      isTokenExpired: (): boolean => options.expired ?? false,
      refreshToken: (): Observable<TokenResponse | null> => {
        refreshCalls.push(1);
        return of(options.refreshResult ?? null);
      },
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: auth },
        {
          provide: ApplicationPathService,
          useValue: { buildHubUrl: (path: string): string => `/base${path}` },
        },
      ],
    });

    return { service: TestBed.inject(AppHubService), refreshCalls };
  }

  async function connected(options: SetupOptions = {}) {
    const context = setup(options);
    await context.service.start();
    return context;
  }

  beforeEach(() => {
    localStorage.clear();
    hub.reset();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    localStorage.clear();
    hub.reset();
  });

  describe('connection lifecycle', () => {
    it('builds the hub against the resolved base path and marks itself connected', async () => {
      const { service } = await connected();

      expect(hub.built.url).toBe('/base/api/hubs/app');
      expect(hub.built.logLevel).toBe('Warning');
      expect(service.isConnected()).toBe(true);
    });

    it('requests the initial payloads once connected', async () => {
      await connected();

      expect(hub.invocations).toEqual([
        { method: 'GetRecentLogs', args: [] },
        { method: 'GetRecentEvents', args: [10] },
        { method: 'GetRecentStrikes', args: [5] },
        { method: 'GetJobStatus', args: [] },
      ]);
    });

    it('builds only one connection when started twice', async () => {
      const { service } = await connected();

      await service.start();

      expect(hub.built.count).toBe(1);
    });

    it('backs off exponentially and caps the reconnect delay', async () => {
      await connected();

      const delay = (previousRetryCount: number) =>
        hub.built.retry?.nextRetryDelayInMilliseconds({ previousRetryCount });

      expect(delay(0)).toBe(2000);
      expect(delay(1)).toBe(4000);
      expect(delay(3)).toBe(16_000);
      expect(delay(10)).toBe(30_000);
    });

    it('drops the connected flag while reconnecting and on close', async () => {
      const { service } = await connected();

      fire('reconnecting');
      expect(service.isConnected()).toBe(false);

      fire('close');
      expect(service.isConnected()).toBe(false);
    });

    it('re-requests the initial payloads after a successful reconnect', async () => {
      const { service } = await connected();
      hub.invocations.length = 0;

      fire('reconnected');

      expect(service.isConnected()).toBe(true);
      expect(methods()).toEqual([
        'GetRecentLogs',
        'GetRecentEvents',
        'GetRecentStrikes',
        'GetJobStatus',
      ]);
    });

    it('retries after the reconnect delay when the initial start fails', async () => {
      const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined);
      vi.useFakeTimers();
      hub.failNextStarts(new Error('boom'));
      const { service } = setup();

      await service.start();

      expect(service.isConnected()).toBe(false);
      expect(hub.built.count).toBe(1);
      expect(warn).toHaveBeenCalled();

      hub.failNextStarts(null);
      await vi.advanceTimersByTimeAsync(2000);

      expect(hub.built.count).toBe(2);
      expect(service.isConnected()).toBe(true);
    });

    it('cancels the pending retry when stopped', async () => {
      vi.spyOn(console, 'warn').mockImplementation(() => undefined);
      vi.useFakeTimers();
      hub.failNextStarts(new Error('boom'));
      const { service } = setup();
      await service.start();

      await service.stop();
      hub.failNextStarts(null);
      await vi.advanceTimersByTimeAsync(10_000);

      expect(hub.built.count).toBe(1);
    });

    it('stops the connection and allows a fresh start afterwards', async () => {
      const { service } = await connected();

      await service.stop();

      expect(hub.stopCount()).toBe(1);
      expect(service.isConnected()).toBe(false);

      await service.start();

      expect(hub.built.count).toBe(2);
    });

    it('swallows invocations while no connection exists', async () => {
      const { service } = await connected();
      await service.stop();
      hub.invocations.length = 0;

      service.requestRecentLogs();

      expect(hub.invocations).toHaveLength(0);
    });

    it('swallows invocations while the connection is not in the connected state', async () => {
      const { service } = await connected();
      hub.invocations.length = 0;
      hub.connection.state = 'Disconnected';

      service.requestRecentLogs();
      service.requestJobStatus();

      expect(hub.invocations).toHaveLength(0);
    });

    it('forwards the requested manual event count', async () => {
      const { service } = await connected();
      hub.invocations.length = 0;

      service.requestRecentManualEvents();
      service.requestRecentManualEvents(25);
      service.requestRecentEvents(3);
      service.requestRecentStrikes(7);

      expect(hub.invocations).toEqual([
        { method: 'GetRecentManualEvents', args: [100] },
        { method: 'GetRecentManualEvents', args: [25] },
        { method: 'GetRecentEvents', args: [3] },
        { method: 'GetRecentStrikes', args: [7] },
      ]);
    });
  });

  describe('access token factory', () => {
    async function token(options: SetupOptions): Promise<string> {
      await connected(options);
      const factory = hub.built.options?.accessTokenFactory;
      if (!factory) {
        throw new Error('no access token factory configured');
      }
      return factory();
    }

    it('skips the refresh entirely when neither an access nor a refresh token exists', async () => {
      const rotated: TokenResponse = { accessToken: 'rotated', refreshToken: 'r', expiresIn: 900 };

      expect(await token({ accessToken: null, expired: true, refreshResult: rotated })).toBe('');
    });

    it('yields the stored token while it is still valid', async () => {
      expect(await token({ accessToken: 'valid', expired: false })).toBe('valid');
    });

    it('refreshes an expired token and yields the rotated one', async () => {
      const rotated: TokenResponse = { accessToken: 'rotated', refreshToken: 'r', expiresIn: 900 };

      expect(await token({ accessToken: 'stale', expired: true, refreshResult: rotated })).toBe(
        'rotated',
      );
    });

    it('yields an empty token when the refresh is rejected', async () => {
      expect(await token({ accessToken: 'stale', expired: true, refreshResult: null })).toBe('');
    });

    it('still attempts a refresh when only a refresh token remains', async () => {
      localStorage.setItem('refresh_token', 'stored');
      const rotated: TokenResponse = { accessToken: 'rotated', refreshToken: 'r', expiresIn: 900 };

      expect(await token({ accessToken: null, expired: true, refreshResult: rotated })).toBe(
        'rotated',
      );
    });
  });

  describe('logs', () => {
    it('prepends a received log entry', async () => {
      const { service } = await connected();

      emit('LogReceived', log('first'));
      emit('LogReceived', log('second'));

      expect(service.logs().map((entry) => entry.message)).toEqual(['second', 'first']);
    });

    it('caps the buffer and drops the oldest entry', async () => {
      const { service } = await connected();

      for (let index = 0; index <= MAX_BUFFER; index++) {
        emit('LogReceived', log(`log-${index}`));
      }

      const messages = service.logs().map((entry) => entry.message);
      expect(messages).toHaveLength(MAX_BUFFER);
      expect(messages[0]).toBe(`log-${MAX_BUFFER}`);
      expect(messages[messages.length - 1]).toBe('log-1');
    });

    it('replaces the buffer with the bulk payload in newest first order', async () => {
      const { service } = await connected();
      emit('LogReceived', log('stale'));

      const bulk = [log('oldest'), log('newest')];
      emit('LogsReceived', bulk);

      expect(service.logs().map((entry) => entry.message)).toEqual(['newest', 'oldest']);
      expect(bulk.map((entry) => entry.message)).toEqual(['oldest', 'newest']);
    });

    it('clears the buffer on demand', async () => {
      const { service } = await connected();
      emit('LogReceived', log('first'));

      service.clearLogs();

      expect(service.logs()).toEqual([]);
    });
  });

  describe('events', () => {
    it('prepends a received event', async () => {
      const { service } = await connected();

      emit('EventReceived', event('a'));
      emit('EventReceived', event('b'));

      expect(service.events().map((item) => item.id)).toEqual(['b', 'a']);
    });

    it('replaces an existing event with the same id and promotes it to the front', async () => {
      const { service } = await connected();
      emit('EventReceived', event('a', 'pending'));
      emit('EventReceived', event('b'));

      emit('EventReceived', event('a', 'completed'));

      expect(service.events().map((item) => item.id)).toEqual(['a', 'b']);
      expect(service.events()[0].message).toBe('completed');
    });

    it('caps the buffer and drops the oldest event', async () => {
      const { service } = await connected();

      for (let index = 0; index <= MAX_BUFFER; index++) {
        emit('EventReceived', event(`event-${index}`));
      }

      const ids = service.events().map((item) => item.id);
      expect(ids).toHaveLength(MAX_BUFFER);
      expect(ids[0]).toBe(`event-${MAX_BUFFER}`);
      expect(ids[ids.length - 1]).toBe('event-1');
    });

    it('replaces the buffer with the bulk payload as received', async () => {
      const { service } = await connected();
      emit('EventReceived', event('stale'));

      emit('EventsReceived', [event('a'), event('b')]);

      expect(service.events().map((item) => item.id)).toEqual(['a', 'b']);
    });

    it('clears the buffer on demand', async () => {
      const { service } = await connected();
      emit('EventReceived', event('a'));

      service.clearEvents();

      expect(service.events()).toEqual([]);
    });
  });

  describe('manual events', () => {
    it('prepends and deduplicates by id', async () => {
      const { service } = await connected();
      emit('ManualEventReceived', manualEvent('a', 'first'));
      emit('ManualEventReceived', manualEvent('b'));

      emit('ManualEventReceived', manualEvent('a', 'updated'));

      expect(service.manualEvents().map((item) => item.id)).toEqual(['a', 'b']);
      expect(service.manualEvents()[0].message).toBe('updated');
    });

    it('caps the buffer and drops the oldest manual event', async () => {
      const { service } = await connected();

      for (let index = 0; index <= MAX_BUFFER; index++) {
        emit('ManualEventReceived', manualEvent(`manual-${index}`));
      }

      const ids = service.manualEvents().map((item) => item.id);
      expect(ids).toHaveLength(MAX_BUFFER);
      expect(ids[0]).toBe(`manual-${MAX_BUFFER}`);
      expect(ids[ids.length - 1]).toBe('manual-1');
    });

    it('replaces the buffer with the bulk payload as received', async () => {
      const { service } = await connected();
      emit('ManualEventReceived', manualEvent('stale'));

      emit('ManualEventsReceived', [manualEvent('a'), manualEvent('b')]);

      expect(service.manualEvents().map((item) => item.id)).toEqual(['a', 'b']);
    });

    it('removes a single manual event by id', async () => {
      const { service } = await connected();
      emit('ManualEventsReceived', [manualEvent('a'), manualEvent('b')]);

      service.removeManualEvent('a');

      expect(service.manualEvents().map((item) => item.id)).toEqual(['b']);
    });

    it('leaves the buffer untouched when removing an unknown id', async () => {
      const { service } = await connected();
      emit('ManualEventsReceived', [manualEvent('a')]);

      service.removeManualEvent('missing');

      expect(service.manualEvents().map((item) => item.id)).toEqual(['a']);
    });

    it('clears the buffer on demand', async () => {
      const { service } = await connected();
      emit('ManualEventReceived', manualEvent('a'));

      service.clearManualEvents();

      expect(service.manualEvents()).toEqual([]);
    });
  });

  describe('strikes', () => {
    it('prepends and deduplicates by id', async () => {
      const { service } = await connected();
      emit('StrikeReceived', strike('a', 'first'));
      emit('StrikeReceived', strike('b'));

      emit('StrikeReceived', strike('a', 'updated'));

      expect(service.strikes().map((item) => item.id)).toEqual(['a', 'b']);
      expect(service.strikes()[0].title).toBe('updated');
    });

    it('caps the buffer and drops the oldest strike', async () => {
      const { service } = await connected();

      for (let index = 0; index <= MAX_BUFFER; index++) {
        emit('StrikeReceived', strike(`strike-${index}`));
      }

      const ids = service.strikes().map((item) => item.id);
      expect(ids).toHaveLength(MAX_BUFFER);
      expect(ids[0]).toBe(`strike-${MAX_BUFFER}`);
      expect(ids[ids.length - 1]).toBe('strike-1');
    });

    it('replaces the buffer with the bulk payload as received', async () => {
      const { service } = await connected();
      emit('StrikeReceived', strike('stale'));

      emit('StrikesReceived', [strike('a'), strike('b')]);

      expect(service.strikes().map((item) => item.id)).toEqual(['a', 'b']);
    });
  });

  describe('jobs and app status', () => {
    it('replaces the job list on a bulk update', async () => {
      const { service } = await connected();
      emit('JobsStatusUpdate', [job('QueueCleaner'), job('MalwareBlocker')]);

      emit('JobsStatusUpdate', [job('DownloadCleaner')]);

      expect(service.jobs().map((item) => item.jobType)).toEqual(['DownloadCleaner']);
    });

    it('updates a single job in place without reordering', async () => {
      const { service } = await connected();
      emit('JobsStatusUpdate', [job('QueueCleaner'), job('MalwareBlocker')]);

      emit('JobStatusUpdate', job('QueueCleaner', 'Running'));

      expect(service.jobs().map((item) => item.jobType)).toEqual([
        'QueueCleaner',
        'MalwareBlocker',
      ]);
      expect(service.jobs()[0].status).toBe('Running');
    });

    it('appends a job that is not yet tracked', async () => {
      const { service } = await connected();
      emit('JobsStatusUpdate', [job('QueueCleaner')]);

      emit('JobStatusUpdate', job('Seeker', 'Running'));

      expect(service.jobs().map((item) => item.jobType)).toEqual(['QueueCleaner', 'Seeker']);
    });

    it('stores the latest app status', async () => {
      const { service } = await connected();

      expect(service.appStatus()).toBeNull();

      emit('AppStatusUpdated', { currentVersion: '2.1.0', latestVersion: '2.2.0' });

      expect(service.appStatus()).toEqual({ currentVersion: '2.1.0', latestVersion: '2.2.0' });
    });
  });

  describe('refresh signals', () => {
    it('bumps the custom format scores version on every notification', async () => {
      const { service } = await connected();

      expect(service.cfScoresVersion()).toBe(0);

      emit('CfScoresUpdated');
      emit('CfScoresUpdated');

      expect(service.cfScoresVersion()).toBe(2);
    });

    it('bumps the search stats version on every notification', async () => {
      const { service } = await connected();

      emit('SearchStatsUpdated');

      expect(service.searchStatsVersion()).toBe(1);
    });
  });
});

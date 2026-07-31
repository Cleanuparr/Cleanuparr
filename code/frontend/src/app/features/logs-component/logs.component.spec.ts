import { WritableSignal, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { AppHubService } from '@core/realtime/app-hub.service';
import { LogEntry } from '@core/models/signalr.models';
import { LogsComponent } from './logs.component';

function entry(timestamp: string, partial: Partial<LogEntry> = {}): LogEntry {
  return {
    level: 'Information',
    message: 'message',
    ...partial,
    timestamp: new Date(timestamp),
  };
}

const LOGS: LogEntry[] = [
  entry('2026-07-30T10:00:00Z', {
    level: 'Information',
    message: 'Queue scan finished',
    category: 'QueueCleaner',
    jobName: 'QueueCleaner',
    jobRunId: 'run-a',
  }),
  entry('2026-07-30T12:00:00Z', {
    level: 'ERROR',
    message: 'Connection refused',
    category: 'Sonarr',
    exception: 'SocketException: refused',
    instanceName: 'Sonarr Main',
    jobName: 'MalwareBlocker',
    jobRunId: 'run-b',
  }),
  entry('2026-07-30T11:00:00Z', {
    level: 'Error',
    message: 'Torrent stalled',
    category: 'QueueCleaner',
    downloadClientType: 'qBittorrent',
    downloadClientName: 'Seedbox',
    jobRunId: 'run-a',
  }),
];

class IntersectionObserverStub {
  observe(): void {
    return undefined;
  }

  unobserve(): void {
    return undefined;
  }

  disconnect(): void {
    return undefined;
  }
}

describe('LogsComponent', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  function setup(
    logs: LogEntry[] = LOGS,
    queryParams: Record<string, string> = {},
  ): { fixture: ComponentFixture<LogsComponent>; logsSignal: WritableSignal<LogEntry[]> } {
    const logsSignal = signal(logs);
    vi.stubGlobal('IntersectionObserver', IntersectionObserverStub);

    TestBed.configureTestingModule({
      providers: [
        {
          provide: AppHubService,
          useValue: {
            logs: logsSignal,
            isConnected: signal(true),
            clearLogs: () => undefined,
            requestRecentLogs: () => undefined,
          },
        },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } },
        },
      ],
    });

    const fixture = TestBed.createComponent(LogsComponent);
    fixture.detectChanges();
    return { fixture, logsSignal };
  }

  function messages(fixture: ComponentFixture<LogsComponent>): string[] {
    return fixture.componentInstance.filteredLogs().map((log) => log.message);
  }

  it('returns every log sorted newest first when no filter is active', () => {
    const { fixture } = setup();

    expect(messages(fixture)).toEqual(['Connection refused', 'Torrent stalled', 'Queue scan finished']);
  });

  it('matches the level filter regardless of the casing stored on the log', () => {
    const { fixture } = setup();

    fixture.componentInstance.selectedLevel.set('error');
    fixture.detectChanges();

    expect(messages(fixture)).toEqual(['Connection refused', 'Torrent stalled']);
  });

  it('searches the message and the other searched fields case-insensitively without failing on absent fields', () => {
    const { fixture } = setup();
    const component = fixture.componentInstance;

    component.searchQuery.set('STALLED');
    fixture.detectChanges();
    expect(messages(fixture)).toEqual(['Torrent stalled']);

    component.searchQuery.set('socketexception');
    fixture.detectChanges();
    expect(messages(fixture)).toEqual(['Connection refused']);

    component.searchQuery.set('sonarr main');
    fixture.detectChanges();
    expect(messages(fixture)).toEqual(['Connection refused']);

    component.searchQuery.set('SEEDBOX');
    fixture.detectChanges();
    expect(messages(fixture)).toEqual(['Torrent stalled']);

    component.searchQuery.set('qbittorrent');
    fixture.detectChanges();
    expect(messages(fixture)).toEqual(['Torrent stalled']);

    component.searchQuery.set('run-a');
    fixture.detectChanges();
    expect(messages(fixture)).toEqual(['Torrent stalled', 'Queue scan finished']);
  });

  it('searches the raw job name and the display name shown on the row', () => {
    const { fixture } = setup();
    const component = fixture.componentInstance;

    component.searchQuery.set('malwareblocker');
    fixture.detectChanges();
    expect(messages(fixture)).toEqual(['Connection refused']);

    component.searchQuery.set('malware blocker');
    fixture.detectChanges();
    expect(messages(fixture)).toEqual(['Connection refused']);

    component.searchQuery.set('queue cleaner');
    fixture.detectChanges();
    expect(messages(fixture)).toEqual(['Queue scan finished']);
  });

  it('narrows the result when several filters are active at once', () => {
    const { fixture } = setup();
    const component = fixture.componentInstance;

    component.selectedCategory.set('QueueCleaner');
    fixture.detectChanges();
    expect(messages(fixture)).toEqual(['Torrent stalled', 'Queue scan finished']);

    component.selectedLevel.set('error');
    fixture.detectChanges();
    expect(messages(fixture)).toEqual(['Torrent stalled']);

    component.searchQuery.set('connection');
    fixture.detectChanges();
    expect(messages(fixture)).toEqual([]);
  });

  it('pre-filters on the job run id taken from the route query parameters', () => {
    const { fixture } = setup(LOGS, { jobRunId: 'run-b' });

    expect(fixture.componentInstance.selectedJobRunId()).toBe('run-b');
    expect(messages(fixture)).toEqual(['Connection refused']);

    fixture.componentInstance.clearJobRunFilter();
    fixture.detectChanges();

    expect(messages(fixture)).toHaveLength(3);
  });

  it('lists deduped sorted categories behind an all-categories entry and skips empty ones', () => {
    const { fixture, logsSignal } = setup();

    logsSignal.set([
      ...LOGS,
      entry('2026-07-30T13:00:00Z', { category: 'Arr' }),
      entry('2026-07-30T14:00:00Z', { category: '' }),
      entry('2026-07-30T15:00:00Z'),
    ]);
    fixture.detectChanges();

    expect(fixture.componentInstance.categoryOptions()).toEqual([
      { label: 'All Categories', value: '' },
      { label: 'Arr', value: 'Arr' },
      { label: 'QueueCleaner', value: 'QueueCleaner' },
      { label: 'Sonarr', value: 'Sonarr' },
    ]);
  });
});

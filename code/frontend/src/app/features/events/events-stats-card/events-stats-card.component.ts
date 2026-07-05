import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import {
  VisXYContainerModule,
  VisLineModule,
  VisAxisModule,
  VisCrosshairModule,
  VisTooltipModule,
  VisBulletLegendModule,
} from '@unovis/angular';
import { Spacing, BulletLegendItemInterface } from '@unovis/ts';
import { CardComponent } from '@ui';
import { EventsApi } from '@core/api/events.api';
import { EventTypeTimelineBucket, EventTypeTimelineResponse } from '@core/models/event.models';

interface WindowOption {
  label: string;
  hours: number;
}

const WINDOWS: WindowOption[] = [
  { label: '24h', hours: 24 },
  { label: '7d', hours: 168 },
  { label: '30d', hours: 720 },
  { label: '1y', hours: 8760 },
];

const TYPE_COLORS: Record<string, string> = {
  QueueItemDeleted: '#ef4444',
  FailedImportStrike: '#f97316',
  StalledStrike: '#f59e0b',
  SlowSpeedStrike: '#eab308',
  SlowTimeStrike: '#84cc16',
  DeadTorrentStrike: '#14b8a6',
  DownloadingMetadataStrike: '#06b6d4',
  CategoryChanged: '#3b82f6',
  SearchTriggered: '#8b5cf6',
  DownloadMarkedForDeletion: '#ec4899',
  DownloadCleaned: '#22c55e',
  StrikeReset: '#10b981',
};

const FALLBACK_COLOR = '#94a3b8';

const EMPTY_TIMELINE: EventTypeTimelineResponse = { types: [], buckets: [] };

@Component({
  selector: 'app-events-stats-card',
  standalone: true,
  imports: [
    CardComponent,
    VisXYContainerModule,
    VisLineModule,
    VisAxisModule,
    VisCrosshairModule,
    VisTooltipModule,
    VisBulletLegendModule,
  ],
  templateUrl: './events-stats-card.component.html',
  styleUrl: './events-stats-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EventsStatsCardComponent {
  private readonly eventsApi = inject(EventsApi);

  readonly windows = WINDOWS;
  readonly window = signal<number>(720);
  readonly hidden = signal<Set<string>>(new Set());

  private readonly timelineResource = rxResource({
    params: () => this.window(),
    stream: ({ params }) => this.eventsApi.getEventTypeTimeline(params),
    defaultValue: EMPTY_TIMELINE,
  });

  readonly isLoading = computed(() => this.timelineResource.isLoading());
  readonly hasError = computed(() => !!this.timelineResource.error());

  readonly data = computed(() => this.timelineResource.value().buckets);
  readonly allTypes = computed(() => this.timelineResource.value().types);
  readonly visibleTypes = computed(() => this.allTypes().filter((t) => !this.hidden().has(t)));
  readonly hasActivity = computed(() => this.allTypes().length > 0);

  readonly yAccessors = computed(() =>
    this.visibleTypes().map((type) => (d: EventTypeTimelineBucket): number => d.counts[type] ?? 0),
  );
  readonly lineColors = computed(() => this.visibleTypes().map((type) => TYPE_COLORS[type] ?? FALLBACK_COLOR));

  readonly legendItems = computed<BulletLegendItemInterface[]>(() =>
    this.allTypes().map((type) => ({
      name: this.formatEventType(type),
      color: TYPE_COLORS[type] ?? FALLBACK_COLOR,
      inactive: this.hidden().has(type),
    })),
  );

  readonly duration =
    typeof matchMedia !== 'undefined' && matchMedia('(prefers-reduced-motion: reduce)').matches ? 0 : 600;
  readonly chartMargin: Spacing = { top: 8, right: 8, bottom: 4, left: 8 };

  readonly x = (_d: EventTypeTimelineBucket, i: number): number => i;

  readonly xTickFormat = (tick: number | Date): string => {
    const index = typeof tick === 'number' ? Math.round(tick) : 0;
    const bucket = this.data()[index];
    return bucket ? this.formatDate(bucket.date) : '';
  };

  readonly tooltip = (d: EventTypeTimelineBucket): string => {
    const rows = this.visibleTypes()
      .filter((type) => (d.counts[type] ?? 0) > 0)
      .map(
        (type) =>
          `<div style="display:flex;gap:6px;align-items:center">` +
          `<span style="width:8px;height:8px;border-radius:50%;background:${TYPE_COLORS[type] ?? FALLBACK_COLOR}"></span>` +
          `<span style="flex:1">${this.formatEventType(type)}</span>` +
          `<b style="font-variant-numeric:tabular-nums">${d.counts[type]}</b></div>`,
      )
      .join('');
    const body = rows || `<div style="color:var(--text-tertiary)">No events</div>`;
    return (
      `<div style="display:flex;flex-direction:column;gap:4px;font-size:12px;min-width:160px">` +
      `<span style="color:var(--text-tertiary)">${this.formatDate(d.date)}</span>${body}</div>`
    );
  };

  readonly onLegendClick = (_item: BulletLegendItemInterface, index: number): void => {
    const type = this.allTypes()[index];
    if (!type) {
      return;
    }
    const next = new Set(this.hidden());
    if (next.has(type)) {
      next.delete(type);
    } else {
      next.add(type);
    }
    this.hidden.set(next);
  };

  private formatEventType(eventType: string): string {
    return eventType.replace(/([A-Z])/g, ' $1').trim();
  }

  private formatDate(date: string): string {
    return new Date(`${date}T00:00:00`).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
  }

  setWindow(hours: number): void {
    this.window.set(hours);
  }

  retry(): void {
    this.timelineResource.reload();
  }
}

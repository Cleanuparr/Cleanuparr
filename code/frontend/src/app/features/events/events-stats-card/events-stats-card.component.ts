import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import {
  VisXYContainerModule,
  VisLineModule,
  VisAreaModule,
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
    VisAreaModule,
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
  readonly window = signal<number>(24);
  readonly selected = signal<string | null>(null);

  private readonly timelineResource = rxResource({
    params: () => this.window(),
    stream: ({ params }) => this.eventsApi.getEventTypeTimeline(params),
    defaultValue: EMPTY_TIMELINE,
  });

  readonly isLoading = computed(() => this.timelineResource.isLoading());
  readonly hasError = computed(() => !!this.timelineResource.error());

  readonly data = computed(() => this.timelineResource.value().buckets);
  readonly allTypes = computed(() => this.timelineResource.value().types);
  readonly hasActivity = computed(() => this.allTypes().length > 0);

  private readonly busiestType = computed(() => {
    const totals: Record<string, number> = {};
    for (const bucket of this.data()) {
      for (const [type, count] of Object.entries(bucket.counts)) {
        totals[type] = (totals[type] ?? 0) + count;
      }
    }
    let best: string | null = null;
    let max = -1;
    for (const type of this.allTypes()) {
      const value = totals[type] ?? 0;
      if (value > max) {
        max = value;
        best = type;
      }
    }
    return best;
  });

  readonly current = computed(() => {
    const selected = this.selected();
    if (selected && this.allTypes().includes(selected)) {
      return selected;
    }
    return this.busiestType();
  });

  readonly currentColor = computed(() => {
    const type = this.current();
    return type ? TYPE_COLORS[type] ?? FALLBACK_COLOR : FALLBACK_COLOR;
  });

  readonly legendItems = computed<BulletLegendItemInterface[]>(() =>
    this.allTypes().map((type) => ({
      name: this.formatEventType(type),
      color: TYPE_COLORS[type] ?? FALLBACK_COLOR,
      inactive: type !== this.current(),
    })),
  );

  readonly y = computed(() => {
    const type = this.current();
    return (d: EventTypeTimelineBucket): number => (type ? d.counts[type] ?? 0 : 0);
  });

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
    const type = this.current();
    const count = type ? d.counts[type] ?? 0 : 0;
    const label = type ? this.formatEventType(type) : '';
    return (
      `<div style="display:flex;flex-direction:column;gap:2px;font-size:12px">` +
      `<span style="color:var(--text-tertiary)">${this.formatDate(d.date)}</span>` +
      `<div style="display:flex;gap:6px;align-items:center">` +
      `<span style="width:8px;height:8px;border-radius:50%;background:${this.currentColor()}"></span>` +
      `<span style="flex:1">${label}</span>` +
      `<b style="font-variant-numeric:tabular-nums">${count}</b></div></div>`
    );
  };

  readonly onLegendClick = (_item: BulletLegendItemInterface, index: number): void => {
    const type = this.allTypes()[index];
    if (type) {
      this.selected.set(type);
    }
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

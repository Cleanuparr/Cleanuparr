import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import {
  VisXYContainerModule,
  VisLineModule,
  VisAreaModule,
  VisAxisModule,
  VisCrosshairModule,
  VisTooltipModule,
} from '@unovis/angular';
import { Spacing, CurveType } from '@unovis/ts';
import { NgIcon } from '@ng-icons/core';
import { CardComponent, TooltipComponent } from '@ui';
import { AnimatedCounterComponent } from '@ui/animated-counter/animated-counter.component';
import { StatsApi } from '@core/api/stats.api';
import { MALWARE_DELETE_REASONS, StatsV2Response, TimelineBucket, TimelineMetric } from '@core/models/stats.models';
import { WINDOWS, getChartDuration, formatBucketDate, chartYDomain } from '@shared/utils/chart-window.util';

interface StatTile {
  key: string;
  label: string;
  hint: string;
  value: number;
  metric?: TimelineMetric; // present tiles drive the chart
  tone: 'removed' | 'recovered' | 'issued' | 'malware' | 'jobs';
}

const METRIC_COLORS: Record<TimelineMetric, string> = {
  removed: 'var(--color-primary)',
  recovered: 'var(--color-success)',
  strikesIssued: 'var(--color-warning)',
  malwareBlocked: 'var(--color-error)',
  events: 'var(--color-primary)',
};

const EMPTY_STATS: StatsV2Response = {
  events: { total: 0, byType: {}, bySeverity: {} },
  strikes: { total: 0, byType: {}, recovered: 0 },
  removals: { total: 0, byReason: {} },
  cleaned: { total: 0, byReason: {} },
  searches: { total: 0, completed: 0, failed: 0, grabbed: 0, byReason: {} },
  jobs: { total: 0, completed: 0, failed: 0, byType: {} },
  windowHours: 0,
  generatedAt: '',
};

@Component({
  selector: 'app-stats-card',
  standalone: true,
  imports: [
    NgIcon,
    TooltipComponent,
    CardComponent,
    AnimatedCounterComponent,
    VisXYContainerModule,
    VisLineModule,
    VisAreaModule,
    VisAxisModule,
    VisCrosshairModule,
    VisTooltipModule,
  ],
  templateUrl: './stats-card.component.html',
  styleUrl: './stats-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatsCardComponent {
  private readonly statsApi = inject(StatsApi);

  readonly windows = WINDOWS;
  readonly window = signal<number>(24);
  readonly selectedMetric = signal<TimelineMetric>('removed');

  private readonly statsResource = rxResource({
    params: () => this.window(),
    stream: ({ params }) => this.statsApi.getStats(params),
    defaultValue: EMPTY_STATS,
  });

  private readonly timelineResource = rxResource({
    params: () => ({ hours: this.window(), metric: this.selectedMetric() }),
    stream: ({ params }) => this.statsApi.getTimeline(params.metric, params.hours),
    defaultValue: [] as TimelineBucket[],
  });

  readonly isLoading = computed(() => this.statsResource.isLoading());
  readonly hasError = computed(() => !!this.statsResource.error());

  readonly tiles = computed<StatTile[]>(() => {
    const s = this.statsResource.value();
    return [
      {
        key: 'removed',
        label: 'Downloads removed',
        hint: 'Downloads deleted from the queue in this window, across all removal reasons.',
        value: s.removals.total,
        metric: 'removed',
        tone: 'removed',
      },
      {
        key: 'recovered',
        label: 'Recovered',
        hint: 'Downloads that resumed healthy progress and had their strikes reset before removal.',
        value: s.strikes.recovered,
        metric: 'recovered',
        tone: 'recovered',
      },
      {
        key: 'issued',
        label: 'Strikes issued',
        hint: 'Strikes handed out to stalled, slow, or failing downloads in this window.',
        value: s.strikes.total,
        metric: 'strikesIssued',
        tone: 'issued',
      },
      {
        key: 'malware',
        label: 'Malware blocked',
        hint: 'Downloads removed because they contained blocked or malicious files.',
        value: MALWARE_DELETE_REASONS.reduce((sum, reason) => sum + (s.removals.byReason[reason] ?? 0), 0),
        metric: 'malwareBlocked',
        tone: 'malware',
      },
      {
        key: 'jobs',
        label: 'Job failures',
        hint: 'Scheduled job runs that failed in this window.',
        value: s.jobs.failed,
        tone: 'jobs',
      },
    ];
  });

  readonly timeline = computed(() => this.timelineResource.value());
  readonly hasActivity = computed(() => this.timeline().some((b) => b.count > 0));
  readonly chartColor = computed(() => METRIC_COLORS[this.selectedMetric()]);

  readonly yDomain = computed<[number, number]>(() => chartYDomain(this.timeline().map((b) => b.count)));

  readonly CurveType = CurveType;
  readonly duration = getChartDuration();
  readonly chartMargin: Spacing = { top: 6, right: 4, bottom: 4, left: 28 };

  readonly x = (_d: TimelineBucket, i: number): number => i;
  readonly y = (d: TimelineBucket): number => d.count;
  readonly yBaseline = computed<number>(() => this.yDomain()[0]);

  readonly yTickFormat = (tick: number | Date): string =>
    typeof tick === 'number' ? Math.round(tick).toString() : '';

  readonly xTickFormat = (tick: number | Date): string => {
    const index = typeof tick === 'number' ? Math.round(tick) : 0;
    const bucket = this.timeline()[index];
    return bucket ? formatBucketDate(bucket.date, this.window()) : '';
  };

  readonly tooltip = (d: TimelineBucket): string =>
    `<div style="display:flex;gap:6px;align-items:center;font-size:12px">` +
    `<span style="color:var(--text-tertiary)">${formatBucketDate(d.date, this.window())}</span>` +
    `<b style="font-variant-numeric:tabular-nums">${d.count}</b></div>`;

  setWindow(hours: number): void {
    this.window.set(hours);
  }

  selectMetric(metric: TimelineMetric | undefined): void {
    if (metric) {
      this.selectedMetric.set(metric);
    }
  }

  retry(): void {
    this.statsResource.reload();
    this.timelineResource.reload();
  }
}

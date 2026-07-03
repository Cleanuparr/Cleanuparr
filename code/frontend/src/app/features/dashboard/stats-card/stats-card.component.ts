import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { CardComponent } from '@ui';
import { AnimatedCounterComponent } from '@ui/animated-counter/animated-counter.component';
import { StatsApi } from '@core/api/stats.api';
import { StatsV2Response, StatsWindow, TimelineBucket, TimelineMetric } from '@core/models/stats.models';

interface StatTile {
  key: string;
  label: string;
  value: number;
  metric?: TimelineMetric; // present tiles drive the sparkline
  tone: 'removed' | 'recovered' | 'issued' | 'malware' | 'jobs';
}

const WINDOWS: StatsWindow[] = ['24h', '7d', '30d', '1y'];

const EMPTY_STATS: StatsV2Response = {
  events: { totalCount: 0, byType: {}, bySeverity: {} },
  strikes: { active: {}, issued: 0, recovered: 0, removed: 0 },
  malware: { blocked: 0 },
  jobs: { totalRuns: 0, completed: 0, failed: 0, byType: {} },
  window: '',
  generatedAt: '',
};

@Component({
  selector: 'app-stats-card',
  standalone: true,
  imports: [CardComponent, AnimatedCounterComponent],
  templateUrl: './stats-card.component.html',
  styleUrl: './stats-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatsCardComponent {
  private readonly statsApi = inject(StatsApi);

  readonly windows = WINDOWS;
  readonly window = signal<StatsWindow>('7d');
  readonly selectedMetric = signal<TimelineMetric>('removed');

  private readonly statsResource = rxResource({
    params: () => this.window(),
    stream: ({ params }) => this.statsApi.getStats(params),
    defaultValue: EMPTY_STATS,
  });

  private readonly timelineResource = rxResource({
    params: () => ({ window: this.window(), metric: this.selectedMetric() }),
    stream: ({ params }) => this.statsApi.getTimeline(params.metric, params.window),
    defaultValue: [] as TimelineBucket[],
  });

  readonly isLoading = computed(() => this.statsResource.isLoading());
  readonly hasError = computed(() => !!this.statsResource.error());

  readonly tiles = computed<StatTile[]>(() => {
    const s = this.statsResource.value();
    return [
      { key: 'removed', label: 'Downloads removed', value: s.strikes.removed, metric: 'removed', tone: 'removed' },
      { key: 'recovered', label: 'Recovered', value: s.strikes.recovered, metric: 'recovered', tone: 'recovered' },
      { key: 'issued', label: 'Strikes issued', value: s.strikes.issued, metric: 'strikesIssued', tone: 'issued' },
      { key: 'malware', label: 'Malware blocked', value: s.malware.blocked, metric: 'malwareBlocked', tone: 'malware' },
      { key: 'jobs', label: 'Job failures', value: s.jobs.failed, tone: 'jobs' },
    ];
  });

  /** Inline SVG sparkline geometry for the selected metric timeline. */
  readonly spark = computed(() => {
    const data = this.timelineResource.value().map((b) => b.count);
    const width = 100;
    const height = 28;
    if (data.length === 0) {
      return { line: '', area: '', max: 0, empty: true };
    }
    const max = Math.max(1, ...data);
    const stepX = data.length > 1 ? width / (data.length - 1) : 0;
    const points = data.map((v, i) => {
      const x = data.length > 1 ? i * stepX : width / 2;
      const y = height - (v / max) * (height - 2) - 1;
      return `${x.toFixed(2)},${y.toFixed(2)}`;
    });
    const line = `M ${points.join(' L ')}`;
    const area = `${line} L ${width},${height} L 0,${height} Z`;
    return { line, area, max, empty: data.every((v) => v === 0) };
  });

  setWindow(window: StatsWindow): void {
    this.window.set(window);
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

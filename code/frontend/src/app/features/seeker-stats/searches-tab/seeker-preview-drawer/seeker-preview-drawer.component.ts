import { Component, ChangeDetectionStrategy, LOCALE_ID, inject, input, model, computed, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { DatePipe, formatDate } from '@angular/common';
import {
  BadgeComponent, ButtonComponent, DrawerComponent, EmptyStateComponent,
  InputComponent, PaginatorComponent, SelectComponent, SpinnerComponent,
} from '@ui';
import type { SelectOption } from '@ui';
import { SearchStatsApi } from '@core/api/search-stats.api';
import { SeekerExclusionReason } from '@core/models/search-stats.models';
import type { InstanceSearchStat, SeekerPreview, SeekerPreviewExclusion } from '@core/models/search-stats.models';
import { EXCLUSION_REASON_LABELS, formatSearchReason, funnelStages, searchReasonSeverity } from '../search-display.util';

const DEFAULT_PAGE_SIZE = 25;

@Component({
  selector: 'app-seeker-preview-drawer',
  standalone: true,
  imports: [
    DatePipe,
    BadgeComponent,
    ButtonComponent,
    DrawerComponent,
    EmptyStateComponent,
    InputComponent,
    PaginatorComponent,
    SelectComponent,
    SpinnerComponent,
  ],
  templateUrl: './seeker-preview-drawer.component.html',
  styleUrl: './seeker-preview-drawer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SeekerPreviewDrawerComponent {
  private readonly api = inject(SearchStatsApi);
  private readonly locale = inject(LOCALE_ID);

  readonly instance = input<InstanceSearchStat | null>(null);
  readonly visible = model(false);

  private readonly previewResource = rxResource({
    params: () => (this.visible() ? this.instance()?.instanceId : undefined),
    stream: ({ params }) => this.api.getPreview(params),
  });

  readonly preview = computed<SeekerPreview | null>(() =>
    this.previewResource.hasValue() ? this.previewResource.value() : null,
  );
  readonly loading = computed(() => this.previewResource.isLoading());
  readonly error = computed(() => this.previewResource.error()?.message ?? null);

  readonly excluded = computed(() => this.preview()?.excluded ?? []);

  readonly stages = computed(() => {
    const preview = this.preview();
    const instance = this.instance();
    if (!preview || !instance) {
      return null;
    }
    const { narrowing, split } = funnelStages(preview.funnel, instance.instanceType);
    return [...narrowing, ...split];
  });

  readonly reasonFilter = linkedSignal<string | undefined, SeekerExclusionReason | ''>({
    source: () => this.instance()?.instanceId,
    computation: () => '',
  });
  readonly titleFilter = linkedSignal<string | undefined, string>({
    source: () => this.instance()?.instanceId,
    computation: () => '',
  });

  readonly reasonOptions = computed<SelectOption[]>(() => {
    const counts = new Map<SeekerExclusionReason, number>();
    for (const item of this.excluded()) {
      counts.set(item.reason, (counts.get(item.reason) ?? 0) + 1);
    }
    return [
      { label: 'All reasons', value: '' },
      ...[...counts].map(([reason, count]) => ({ label: `${EXCLUSION_REASON_LABELS[reason]} (${count})`, value: reason })),
    ];
  });

  readonly filteredExcluded = computed(() => {
    const reason = this.reasonFilter();
    const title = this.titleFilter().trim().toLowerCase();
    return this.excluded().filter(
      (item) => (!reason || item.reason === reason) && (!title || item.name.toLowerCase().includes(title)),
    );
  });

  readonly pageSize = linkedSignal(() => {
    this.instance();
    return DEFAULT_PAGE_SIZE;
  });
  readonly page = linkedSignal(() => {
    this.filteredExcluded();
    this.pageSize();
    return 1;
  });

  readonly pagedExcluded = computed(() => {
    const start = (this.page() - 1) * this.pageSize();
    return this.filteredExcluded().slice(start, start + this.pageSize());
  });

  readonly reasonLabels = EXCLUSION_REASON_LABELS;
  readonly formatSearchReason = formatSearchReason;
  readonly searchReasonSeverity = searchReasonSeverity;

  onReasonChange(value: unknown): void {
    this.reasonFilter.set(value as SeekerExclusionReason | '');
  }

  restDetail(item: SeekerPreviewExclusion): string {
    if (item.reason !== SeekerExclusionReason.Resting) {
      return '';
    }
    if (item.restCyclesLeft == null || item.consecutiveMisses == null) {
      return '';
    }
    const cycles = item.restCyclesLeft;
    const until = item.restUntil ? ` or on ${formatDate(item.restUntil, 'MM/dd', this.locale)}` : '';
    return `${item.consecutiveMisses} misses in a row · back after ${cycles} ${cycles === 1 ? 'cycle ends' : 'cycles end'}${until}`;
  }

  refresh(): void {
    this.previewResource.reload();
  }
}

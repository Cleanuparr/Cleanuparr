import { Component, ChangeDetectionStrategy, inject, signal, computed, effect, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { form, min, max, FormField } from '@angular/forms/signals';
import { DatePipe } from '@angular/common';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, ToggleComponent,
  SelectComponent, ChipInputComponent, NumberInputComponent,
  EmptyStateComponent, LoadingStateComponent, BadgeComponent,
  type SelectOption, type BadgeSeverity,
} from '@ui';
import { SeekerApi } from '@core/api/seeker.api';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@core/services/confirm.service';
import { UpdateSeekerConfig } from '@shared/models/seeker-config.model';
import { HasPendingChanges } from '@core/guards/pending-changes.guard';
import { ApiError } from '@core/interceptors/error.interceptor';
import { DeferredLoader } from '@shared/utils/loading.util';
import { SelectionStrategy } from '@shared/models/enums';

const INTERVAL_OPTIONS: SelectOption[] = [
  { label: '2 minutes', value: 2 },
  { label: '3 minutes', value: 3 },
  { label: '4 minutes', value: 4 },
  { label: '5 minutes', value: 5 },
  { label: '6 minutes', value: 6 },
  { label: '10 minutes', value: 10 },
  { label: '12 minutes', value: 12 },
  { label: '15 minutes', value: 15 },
  { label: '20 minutes', value: 20 },
  { label: '30 minutes', value: 30 },
  { label: '1 hour', value: 60 },
  { label: '2 hours', value: 120 },
  { label: '3 hours', value: 180 },
  { label: '4 hours', value: 240 },
  { label: '6 hours', value: 360 },
];

const STRATEGY_OPTIONS: SelectOption[] = [
  { label: 'Balanced Weighted', value: SelectionStrategy.BalancedWeighted },
  { label: 'Oldest Search First', value: SelectionStrategy.OldestSearchFirst },
  { label: 'Oldest Search Weighted', value: SelectionStrategy.OldestSearchWeighted },
  { label: 'Newest First', value: SelectionStrategy.NewestFirst },
  { label: 'Newest Weighted', value: SelectionStrategy.NewestWeighted },
  { label: 'Random', value: SelectionStrategy.Random },
];

const STRATEGY_DESCRIPTIONS: Record<SelectionStrategy, string> = {
  [SelectionStrategy.BalancedWeighted]: 'Prioritizes items that are both newly added and haven\'t been searched recently. Good default for most libraries.',
  [SelectionStrategy.OldestSearchFirst]: 'Works through your library in order, starting with items that haven\'t been searched the longest. Guarantees every item gets covered.',
  [SelectionStrategy.OldestSearchWeighted]: 'Favors items that haven\'t been searched recently, but still gives other items a chance.',
  [SelectionStrategy.NewestFirst]: 'Always picks the most recently added items first. Best for keeping new additions up to date quickly.',
  [SelectionStrategy.NewestWeighted]: 'Favors recently added items, but still gives older items a chance.',
  [SelectionStrategy.Random]: 'Every item has an equal chance of being picked. No prioritization.',
};

interface SeekerFormModel {
  searchEnabled: boolean;
  searchInterval: number;
  proactiveSearchEnabled: boolean;
  selectionStrategy: SelectionStrategy;
  useRoundRobin: boolean;
  postReleaseGraceHours: number | null;
}

interface InstanceState {
  arrInstanceId: string;
  instanceName: string;
  instanceType: string;
  enabled: boolean;
  skipTags: string[];
  lastProcessedAt?: string;
  arrInstanceEnabled: boolean;
  activeDownloadLimit: number;
  minCycleTimeDays: number;
  monitoredOnly: boolean;
  useCutoff: boolean;
  useCustomFormatScore: boolean;
}

@Component({
  selector: 'app-seeker',
  standalone: true,
  imports: [
    PageHeaderComponent, CardComponent, ButtonComponent,
    ToggleComponent, SelectComponent, ChipInputComponent, NumberInputComponent,
    EmptyStateComponent, LoadingStateComponent, BadgeComponent, DatePipe, FormField,
  ],
  templateUrl: './seeker.component.html',
  styleUrl: './seeker.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SeekerComponent implements HasPendingChanges {
  private readonly api = inject(SeekerApi);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);

  private readonly savedSnapshot = signal('');

  private readonly configResource = rxResource({
    stream: () => this.api.getConfig(),
  });

  readonly intervalOptions = INTERVAL_OPTIONS;
  readonly strategyOptions = STRATEGY_OPTIONS;
  readonly loader = new DeferredLoader();
  readonly loadError = computed(() => !!this.configResource.error());
  readonly saving = signal(false);
  readonly saved = signal(false);

  // Scalar settings live in a Signal Forms model; the per-instance array stays a
  // signal (one-way bound rows with imperative updates) and is not a form field.
  private readonly model = signal<SeekerFormModel>({
    searchEnabled: true,
    searchInterval: 2,
    proactiveSearchEnabled: false,
    selectionStrategy: SelectionStrategy.BalancedWeighted,
    useRoundRobin: true,
    postReleaseGraceHours: 6,
  });

  readonly seekerForm = form(this.model, (p) => {
    min(p.postReleaseGraceHours, 0);
    max(p.postReleaseGraceHours, 72);
  });

  readonly instances = signal<InstanceState[]>([]);

  readonly strategyDescription = computed(() => STRATEGY_DESCRIPTIONS[this.model().selectionStrategy] ?? '');

  readonly instanceError = computed(() => {
    if (this.model().proactiveSearchEnabled && this.instances().length > 0 && !this.instances().some(i => i.enabled)) {
      return 'At least one instance must be enabled when proactive search is enabled';
    }
    return undefined;
  });

  readonly hasErrors = computed(() => this.seekerForm().invalid() || !!this.instanceError());

  constructor() {
    effect(() => {
      const config = this.configResource.hasValue() ? this.configResource.value() : undefined;
      if (!config) {
        return;
      }
      untracked(() => {
        this.model.set({
          searchEnabled: config.searchEnabled,
          searchInterval: config.searchInterval,
          proactiveSearchEnabled: config.proactiveSearchEnabled,
          selectionStrategy: config.selectionStrategy,
          useRoundRobin: config.useRoundRobin,
          postReleaseGraceHours: config.postReleaseGraceHours,
        });
        this.instances.set(config.instances.map(i => ({
          arrInstanceId: i.arrInstanceId,
          instanceName: i.instanceName,
          instanceType: i.instanceType,
          enabled: i.enabled,
          skipTags: [...i.skipTags],
          lastProcessedAt: i.lastProcessedAt,
          arrInstanceEnabled: i.arrInstanceEnabled,
          activeDownloadLimit: i.activeDownloadLimit,
          minCycleTimeDays: i.minCycleTimeDays,
          monitoredOnly: i.monitoredOnly,
          useCutoff: i.useCutoff,
          useCustomFormatScore: i.useCustomFormatScore,
        })));
        this.savedSnapshot.set(this.buildSnapshot());
      });
    });

    effect(() => {
      if (this.configResource.error()) {
        this.toast.error('Failed to load seeker settings');
      }
    });

    effect(() => {
      if (this.configResource.isLoading()) {
        this.loader.start();
      } else {
        this.loader.stop();
      }
    });
  }

  retry(): void {
    this.configResource.reload();
  }

  readonly confirmRoundRobin = async (newValue: boolean): Promise<boolean> => {
    if (!newValue) {
      return this.confirm.confirm({
        title: 'Disable Round Robin',
        message: 'Disabling round robin will trigger a search for each enabled arr instance per run. This could result in too many requests to your indexers and potentially get you banned.',
        confirmLabel: 'Disable',
        destructive: true,
      });
    }
    return true;
  };

  toggleInstance(index: number): void {
    this.instances.update(instances => {
      const updated = [...instances];
      updated[index] = { ...updated[index], enabled: !updated[index].enabled };
      return updated;
    });
  }

  updateInstanceSkipTags(index: number, tags: string[]): void {
    this.instances.update(instances => {
      const updated = [...instances];
      updated[index] = { ...updated[index], skipTags: tags };
      return updated;
    });
  }

  updateInstanceActiveDownloadLimit(index: number, limit: number | null): void {
    this.instances.update(instances => {
      const updated = [...instances];
      updated[index] = { ...updated[index], activeDownloadLimit: limit ?? 3 };
      return updated;
    });
  }

  updateInstanceMinCycleTimeDays(index: number, days: number | null): void {
    this.instances.update(instances => {
      const updated = [...instances];
      updated[index] = { ...updated[index], minCycleTimeDays: days ?? 7 };
      return updated;
    });
  }

  updateInstanceMonitoredOnly(index: number, value: boolean): void {
    this.instances.update(instances => {
      const updated = [...instances];
      updated[index] = { ...updated[index], monitoredOnly: value };
      return updated;
    });
  }

  updateInstanceUseCutoff(index: number, value: boolean): void {
    this.instances.update(instances => {
      const updated = [...instances];
      updated[index] = { ...updated[index], useCutoff: value };
      return updated;
    });
  }

  updateInstanceUseCustomFormatScore(index: number, value: boolean): void {
    this.instances.update(instances => {
      const updated = [...instances];
      updated[index] = { ...updated[index], useCustomFormatScore: value };
      return updated;
    });
  }

  getInstanceIcon(instanceType: string): string {
    return `icons/ext/${instanceType.toLowerCase()}-light.svg`;
  }

  getInstanceTypeSeverity(type: string): BadgeSeverity {
    if (type === 'Radarr') return 'warning';
    if (type === 'Sonarr') return 'info';
    return 'default';
  }

  save(): void {
    const m = this.model();
    const config: UpdateSeekerConfig = {
      searchEnabled: m.searchEnabled,
      searchInterval: m.searchInterval ?? 2,
      proactiveSearchEnabled: m.proactiveSearchEnabled,
      selectionStrategy: m.selectionStrategy,
      useRoundRobin: m.useRoundRobin,
      postReleaseGraceHours: m.postReleaseGraceHours ?? 6,
      instances: this.instances().map(i => ({
        arrInstanceId: i.arrInstanceId,
        enabled: i.enabled,
        skipTags: i.skipTags,
        activeDownloadLimit: i.activeDownloadLimit,
        minCycleTimeDays: i.minCycleTimeDays,
        monitoredOnly: i.monitoredOnly,
        useCutoff: i.useCutoff,
        useCustomFormatScore: i.useCustomFormatScore,
      })),
    };

    this.saving.set(true);
    this.api.updateConfig(config).subscribe({
      next: () => {
        this.toast.success('Seeker settings saved');
        this.saving.set(false);
        this.saved.set(true);
        setTimeout(() => this.saved.set(false), 1500);
        this.savedSnapshot.set(this.buildSnapshot());
      },
      error: (err: ApiError) => {
        this.toast.error(err.statusCode === 400
          ? err.message
          : 'Failed to save seeker settings');
        this.saving.set(false);
      },
    });
  }

  private buildSnapshot(): string {
    return JSON.stringify({
      ...this.model(),
      instances: [...this.instances()].sort((a, b) => a.arrInstanceId.localeCompare(b.arrInstanceId)),
    });
  }

  readonly dirty = computed(() => {
    const saved = this.savedSnapshot();
    return saved !== '' && saved !== this.buildSnapshot();
  });

  hasPendingChanges(): boolean {
    return this.dirty();
  }
}

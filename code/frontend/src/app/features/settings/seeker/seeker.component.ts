import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, ToggleComponent,
  SelectComponent, ChipInputComponent, NumberInputComponent,
  EmptyStateComponent, LoadingStateComponent,
  type SelectOption,
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

interface InstanceState {
  arrInstanceId: string;
  instanceName: string;
  instanceType: string;
  enabled: boolean;
  skipTags: string[];
  lastProcessedAt?: string;
  arrInstanceEnabled: boolean;
  activeDownloadLimit: number;
}

@Component({
  selector: 'app-seeker',
  standalone: true,
  imports: [
    PageHeaderComponent, CardComponent, ButtonComponent,
    ToggleComponent, SelectComponent, ChipInputComponent, NumberInputComponent,
    EmptyStateComponent, LoadingStateComponent, DatePipe,
  ],
  templateUrl: './seeker.component.html',
  styleUrl: './seeker.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SeekerComponent implements OnInit, HasPendingChanges {
  private readonly api = inject(SeekerApi);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);

  private readonly savedSnapshot = signal('');

  readonly intervalOptions = INTERVAL_OPTIONS;
  readonly strategyOptions = STRATEGY_OPTIONS;
  readonly loader = new DeferredLoader();
  readonly loadError = signal(false);
  readonly saving = signal(false);
  readonly saved = signal(false);

  readonly searchEnabled = signal(true);
  readonly searchInterval = signal<unknown>(2);
  readonly proactiveSearchEnabled = signal(false);
  readonly selectionStrategy = signal<unknown>(SelectionStrategy.BalancedWeighted);
  readonly monitoredOnly = signal(true);
  readonly useCutoff = signal(false);
  readonly useCustomFormatScore = signal(false);
  readonly useRoundRobin = signal(true);

  readonly instances = signal<InstanceState[]>([]);

  readonly strategyDescription = computed(() => STRATEGY_DESCRIPTIONS[this.selectionStrategy() as SelectionStrategy] ?? '');

  readonly instanceError = computed(() => {
    if (this.proactiveSearchEnabled() && this.instances().length > 0 && !this.instances().some(i => i.enabled)) {
      return 'At least one instance must be enabled when proactive search is enabled';
    }
    return undefined;
  });

  readonly hasErrors = computed(() => !!this.instanceError());

  ngOnInit(): void {
    this.loadConfig();
  }

  private loadConfig(): void {
    this.loader.start();
    this.api.getConfig().subscribe({
      next: (config) => {
        this.searchEnabled.set(config.searchEnabled);
        this.searchInterval.set(config.searchInterval);
        this.proactiveSearchEnabled.set(config.proactiveSearchEnabled);
        this.selectionStrategy.set(config.selectionStrategy);
        this.monitoredOnly.set(config.monitoredOnly);
        this.useCutoff.set(config.useCutoff);
        this.useCustomFormatScore.set(config.useCustomFormatScore);
        this.useRoundRobin.set(config.useRoundRobin);
        this.instances.set(config.instances.map(i => ({
          arrInstanceId: i.arrInstanceId,
          instanceName: i.instanceName,
          instanceType: i.instanceType,
          enabled: i.enabled,
          skipTags: [...i.skipTags],
          lastProcessedAt: i.lastProcessedAt,
          arrInstanceEnabled: i.arrInstanceEnabled,
          activeDownloadLimit: i.activeDownloadLimit,
        })));
        this.loader.stop();
        this.savedSnapshot.set(this.buildSnapshot());
      },
      error: () => {
        this.toast.error('Failed to load seeker settings');
        this.loader.stop();
        this.loadError.set(true);
      },
    });
  }

  retry(): void {
    this.loadError.set(false);
    this.loadConfig();
  }

  async toggleRoundRobin(newValue: boolean): Promise<void> {
    if (!newValue) {
      const confirmed = await this.confirm.confirm({
        title: 'Disable Round Robin',
        message: 'Disabling round robin will trigger a search for each enabled arr instance per run. This could result in too many requests to your indexers and potentially get you banned.',
        confirmLabel: 'Disable',
        destructive: true,
      });
      if (!confirmed) {
        // The toggle already flipped its internal state to false.
        // Sync our signal to false first, then restore to true in the next tick
        // so Angular detects an actual change and pushes it back to the toggle.
        this.useRoundRobin.set(false);
        setTimeout(() => this.useRoundRobin.set(true));
        return;
      }
    }
    this.useRoundRobin.set(newValue);
  }

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

  getInstanceIcon(instanceType: string): string {
    return `icons/ext/${instanceType.toLowerCase()}-light.svg`;
  }

  save(): void {
    const config: UpdateSeekerConfig = {
      searchEnabled: this.searchEnabled(),
      searchInterval: (this.searchInterval() as number) ?? 2,
      proactiveSearchEnabled: this.proactiveSearchEnabled(),
      selectionStrategy: this.selectionStrategy() as SelectionStrategy,
      monitoredOnly: this.monitoredOnly(),
      useCutoff: this.useCutoff(),
      useCustomFormatScore: this.useCustomFormatScore(),
      useRoundRobin: this.useRoundRobin(),
      instances: this.instances().map(i => ({
        arrInstanceId: i.arrInstanceId,
        enabled: i.enabled,
        skipTags: i.skipTags,
        activeDownloadLimit: i.activeDownloadLimit,
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
      searchEnabled: this.searchEnabled(),
      searchInterval: this.searchInterval(),
      proactiveSearchEnabled: this.proactiveSearchEnabled(),
      selectionStrategy: this.selectionStrategy(),
      monitoredOnly: this.monitoredOnly(),
      useCutoff: this.useCutoff(),
      useCustomFormatScore: this.useCustomFormatScore(),
      useRoundRobin: this.useRoundRobin(),
      instances: this.instances(),
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
